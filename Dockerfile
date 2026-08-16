# syntax=docker/dockerfile:1

# ---- Stage 1: build the React UI into the host's wwwroot ----
FROM node:24-alpine AS web-build
WORKDIR /src/web
COPY web/package*.json ./
RUN npm ci
COPY web/ ./
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
RUN dotnet restore src/AgentContext.Host/AgentContext.Host.csproj
COPY src/ ./src/
COPY --from=web-build /src/src/AgentContext.Host/wwwroot ./src/AgentContext.Host/wwwroot
RUN dotnet publish src/AgentContext.Host/AgentContext.Host.csproj -c Release -o /app/publish -p:SkipSpaBuild=true

# ---- Stage 3: runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
# Listen on 80 (HTTP default) so OrbStack domains like
# http://Host.agent-context.orb.local work without a port.
ENV ASPNETCORE_URLS=http://+:80
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 80
ENTRYPOINT ["dotnet", "AgentContext.Host.dll", "--web"]
