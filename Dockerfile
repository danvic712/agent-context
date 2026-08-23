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
COPY AgentContext.slnx ./
COPY Directory.Packages.props ./
# The localization store (ADR 0008): embedded by the Application project.
COPY i18n/ ./i18n/
COPY src/AgentContext.Domain/AgentContext.Domain.csproj src/AgentContext.Domain/
COPY src/AgentContext.Infrastructure/AgentContext.Infrastructure.csproj src/AgentContext.Infrastructure/
COPY src/AgentContext.Application/AgentContext.Application.csproj src/AgentContext.Application/
COPY src/AgentContext.Host/AgentContext.Host.csproj src/AgentContext.Host/
# Keep the NuGet graph in a BuildKit cache across builds.
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet restore src/AgentContext.Host/AgentContext.Host.csproj
COPY src/ ./src/
COPY --from=web-build /src/src/AgentContext.Host/wwwroot ./src/AgentContext.Host/wwwroot
RUN --mount=type=cache,target=/root/.nuget/packages \
    dotnet publish src/AgentContext.Host/AgentContext.Host.csproj -c Release -o /app/publish -p:SkipSpaBuild=true --no-restore

# ---- Stage 3: runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# The image runs the ASP.NET Core portal directly. PostgreSQL remains an external
# dependency supplied through ConnectionStrings__Default (for example by Compose).
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "AgentContext.Host.dll"]
