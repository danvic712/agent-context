# Agent Context

A shared context layer for AI agents — manage skills, memory, sessions, and knowledge across personal, family, and team workspaces. Self-hosted, .NET + React, MCP-first.

> Canonical design docs: [`docs/spec.md`](docs/spec.md) · Terminology: [`CONTEXT.md`](CONTEXT.md) · Decisions: [`docs/adr/`](docs/adr/)

## Quick start

```bash
docker compose up -d --build
```

This starts everything with no manual steps:

| Service | Address | Notes |
|---|---|---|
| Web app (`--web`) | http://localhost:8080 | REST API + React UI (shadcn/ui); applies EF Core migrations at startup |
| Postgres (pgvector) | localhost:5432 | `agent_context` / `agent_context` |
| MCP server (`--mcp-stdio`) | stdio | used by Craft Agents as a local stdio source |

Open http://localhost:8080 and the **first-run wizard** creates your admin account
and a Personal Workspace. Rerunning the wizard is blocked once configured.

## Architecture

One .NET project, two entrypoints ([ADR 0006](docs/adr/0006-single-project-dual-mode.md)):

- `dotnet run -- --web` — ASP.NET Core host: REST API (`/api/*`), React UI, health at `/api/health`.
- `dotnet run -- --mcp-stdio` — MCP server over stdio for Craft Agents. Both entrypoints share one DI graph (`AddApplicationServices`).

```
Users (React UI)
      │
      ▼
┌───────────────────────────────────────┐
│  AgentContext (single .NET project)   │
│  --web: REST API + UI                 │
│  --mcp-stdio: MCP over stdio          │
│  shared: EF Core / services / Mcp     │
└──────────────────┬────────────────────┘
                   │
        PostgreSQL (+ pgvector)
```

## Development

```bash
# API + UI (UI served from wwwroot; rebuilt by dotnet build via the SPA target)
dotnet run --project src/AgentContext -- --web

# UI dev server with /api proxied to http://localhost:8080
cd src/AgentContext/web && npm run dev
```

Tests run against a real Postgres with pgvector via Testcontainers:

```bash
dotnet test
```

## Repository layout

```
src/AgentContext/        single dual-mode project
  Controllers/           REST API endpoints
  Application/           AddApplicationServices + application services (primary test seam)
  Domain/                MVP entities (Workspace/Domain/User/Membership/Session/Knowledge/Skill/Usage)
  Infrastructure/        EF Core DbContext + migrations
  Mcp/                   stdio MCP host + tools
  web/                   React UI (Vite + TS + shadcn/ui), built into wwwroot
tests/AgentContext.Tests seam tests (application services against Testcontainers pgvector) + adapter smoke tests
docker-compose.yml       app + Postgres(pgvector)
```
