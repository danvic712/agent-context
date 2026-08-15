# syntax=docker/dockerfile:1

# ---- Stage 1: build the React UI into wwwroot ----
FROM node:24-alpine AS web-build
WORKDIR /src/web
COPY web/package*.json ./
RUN npm ci
COPY web/ ./
# Vite outDir is ../wwwroot inside the project.
RUN npm run build

# ---- Stage 2: publish the .NET app (SkipSpaBuild=true — UI is prebuilt) ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY AgentContext.csproj ./
RUN dotnet restore
COPY . .
COPY --from=web-build /src/wwwroot ./wwwroot
RUN dotnet publish -c Release -o /app/publish -p:SkipSpaBuild=true

# ---- Stage 3: runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production
EXPOSE 8080
ENTRYPOINT ["dotnet", "AgentContext.dll", "--web"]
