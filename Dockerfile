# syntax=docker/dockerfile:1

# ---- Stage 1: build the React UI into the host's wwwroot ----
FROM node:24-alpine AS web-build
WORKDIR /src/web
COPY web/package*.json ./
# --mount=type=cache keeps the npm registry cache across builds so CI-style
# re-runs skip the download (BuildKit).
RUN --mount=type=cache,target=/root/.npm npm ci
COPY web/ ./
# The i18n store lives at the repo root (ADR 0008); web/ imports it via ../../.
COPY i18n/ ../i18n/
# Vite outDir is ../src/AgentContext.Host/wwwroot.
RUN npm run build

# ---- Stage 2: publish the .NET host (SkipSpaBuild=true — UI is prebuilt) ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
ARG TARGETARCH
COPY AgentContext.slnx ./
COPY Directory.Packages.props ./
# The localization store (ADR 0008): embedded by the Application project.
COPY i18n/ ./i18n/
COPY src/AgentContext.Domain/AgentContext.Domain.csproj src/AgentContext.Domain/
COPY src/AgentContext.Infrastructure/AgentContext.Infrastructure.csproj src/AgentContext.Infrastructure/
COPY src/AgentContext.Application/AgentContext.Application.csproj src/AgentContext.Application/
COPY src/AgentContext.Host/AgentContext.Host.csproj src/AgentContext.Host/
# Keep the large NuGet graph in a target-architecture cache. The cache is
# mounted again during publish because runtime-only Aspire packages must be
# staged from it, but they should not become part of the build image layer.
RUN --mount=type=cache,id=nuget-${TARGETARCH},target=/root/.nuget/packages \
    dotnet restore src/AgentContext.Host/AgentContext.Host.csproj
COPY src/ ./src/
COPY --from=web-build /src/src/AgentContext.Host/wwwroot ./src/AgentContext.Host/wwwroot
# The Aspire runtime loads DCP + the in-process dashboard from the NuGet package
# cache at startup (they are NOT referenced by deps.json, so publish omits them).
# Stage only the runtime cache payload outside /app/publish so the final image
# does not contain a duplicate copy. Picks the restored package version
# dynamically while preserving the <version>/ layer the runtime lookup expects.
RUN --mount=type=cache,id=nuget-${TARGETARCH},target=/root/.nuget/packages \
    dotnet publish src/AgentContext.Host/AgentContext.Host.csproj -c Release -o /app/publish -p:SkipSpaBuild=true --no-restore && \
    mkdir -p /tmp/aspire-tools && \
    for pkg in aspire.hosting.orchestration aspire.dashboard.sdk; do \
      dir="$(ls -d /root/.nuget/packages/$pkg.linux-${TARGETARCH}/*/ 2>/dev/null | sort -V | tail -1)"; \
      if [ -z "$dir" ]; then \
        echo "ERROR: $pkg.linux-${TARGETARCH} not found in NuGet cache" >&2; \
        exit 1; \
      fi; \
      mkdir -p "/tmp/aspire-tools/$pkg.linux-${TARGETARCH}"; \
      # strip the trailing slash so cp copies the version DIRECTORY itself, \
      # preserving the <version>/ layer the runtime's cache lookup expects \
      cp -r "${dir%/}" "/tmp/aspire-tools/$pkg.linux-${TARGETARCH}/"; \
    done

# ---- Stage 3: runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# Reconstruct the NuGet cache paths the Aspire runtime probes for DCP + dashboard
# (same layout as a `dotnet restore` global package cache). The build stage
# staged them under /tmp/aspire-tools/<package>/<version>/, so the final image
# receives only the required runtime cache without copying the staging directory.
COPY --from=build /tmp/aspire-tools/ /root/.nuget/packages/
# T15 (issue #15): the image is complete — running it starts the full AppHost
# orchestration (the no-args default): the portal child process (UI + REST + MCP
# /mcp on :8080) plus the in-process Aspire dashboard (:18888, fixed port with
# the AppHost-only Resources view). Postgres stays external (docker compose
# provides it, or any PostgreSQL) via ConnectionStrings__Default. DCP + the
# dashboard binaries ship in the publish output (Aspire AppHost SDK).
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080 18888
ENTRYPOINT ["dotnet", "AgentContext.Host.dll"]
