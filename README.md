# Agent Context

A shared context layer for AI agents — manage skills, memory, sessions, and knowledge across personal, family, and team workspaces. Self-hosted, .NET + React, MCP-first.

> Canonical design docs: [`docs/spec.md`](docs/spec.md) · Terminology: [`CONTEXT.md`](CONTEXT.md) · Decisions: [`docs/adr/`](docs/adr/) · Validation history: [`docs/validation/`](docs/validation/)

## Features

- **MCP gateway** — five v1 tools over stdio for Craft Agents (`save_session`, `search_memory`, `find_similar_solution`, `get_skill`, `rate_knowledge`) + `skill://{domain}/{slug}/{file}` resources.
- **Learning Engine** — a background pipeline turns agent session summaries into domain-scoped, confidence-scored Knowledge (Problem / Solution / Pattern) via a configurable OpenAI-compatible LLM endpoint; embeddings land in pgvector.
- **Retrieval** — domain-scoped, cosine-ranked search with a Confidence threshold; conflict groups surfaced side by side.
- **Knowledge management** — review / archive / restore, temporal decay hygiene, private markers, usage feedback (`rate_knowledge`).
- **Skills** — versioned per `(domain, slug)`, stored as filesystem **packages** (file tree + `SKILL.md` + assets) with an in-browser file editor, drag-drop uploads and zip import.
- **Analytics** — sessions / tokens / cost by workspace, domain and agent; maintained model pricing table.
- **Settings** — LLM endpoint, platform language (`en-US` / `zh-CN`), and color theme (`light` / `dark` / `system`), all persisted in the DB and applied without restart.
- **Localization** — full platform UI + backend errors in the configured language, one JSON store per locale (ADR 0008).
- **Observability** — OpenTelemetry logs + traces + metrics exported to an [Aspire dashboard](http://localhost:18888) by default; `service.name=agent-context`.

## Quick start

```bash
docker compose up -d --build
```

This starts everything with no manual steps:

| Service | Address | Notes |
|---|---|---|
| Web app (`--web`) | http://localhost:8080 | REST API + React UI (botanical blue theme, i18n); applies EF Core migrations at startup |
| Postgres (pgvector) | localhost:5432 | `agent_context` / `agent_context` |
| Aspire dashboard | http://localhost:18888 | OTel logs/traces/metrics viewer (OTLP on 4317/4318); added in T13 |
| MCP server (`--mcp-stdio`) | stdio | used by Craft Agents as a local stdio source |

Open http://localhost:8080 and the **first-run wizard** creates your admin account
and a Personal Workspace (language → account → optional LLM endpoint). Rerunning
the wizard is blocked once configured.

Prebuilt images (`ghcr.io/danvic712/agent-context:latest`, multi-arch
linux/amd64 + arm64) are published by the [release workflow](.github/workflows/release.yml)
on every `v*` tag; `docker compose up -d` pulls them automatically. The local
`build:` block is kept for source builds.

## Architecture

One .NET project, three entrypoints ([ADR 0006](docs/adr/0006-single-project-dual-mode.md)):

- `dotnet run -- --web` (default) — ASP.NET Core host: REST API (`/api/*`), React UI, health at `/api/health`.
- `dotnet run -- --mcp-stdio` — MCP server over stdio for Craft Agents.
- `dotnet run -- --apphost` — Aspire DistributedApplication: models the platform
  as postgres + portal resources, so the dashboard gains the **Resources** view
  (see [`docs/guides/apphost-mode.md`](docs/guides/apphost-mode.md)).

All entrypoints share one DI graph (`AddApplicationServices`).

```
Users (React UI, botanical theme)
      │
      ▼
┌───────────────────────────────────────┐
│  AgentContext (single .NET project)   │
│  --web: REST API + UI                 │
│  --mcp-stdio: MCP over stdio          │
│  --apphost: Aspire DistributedApp     │
│  shared: EF Core / services / Mcp     │
└───────┬───────────────┬───────────────┘
        │ OTLP          │ SQL
        ▼               ▼
  Aspire dashboard  PostgreSQL (+ pgvector)
  (logs/traces/metrics)
```

## Development

```bash
# API + UI (UI served from wwwroot; rebuilt by dotnet build via the SPA target)
dotnet run --project src/AgentContext.Host -- --web

# UI dev server with /api proxied to http://localhost:8080
cd web && npm run dev

# Aspire AppHost mode (dashboard with Resources view; requires Aspire SDK in the csproj)
dotnet run --project src/AgentContext.Host -- --apphost
```

Tests run against a real Postgres with pgvector via Testcontainers (Docker required):

```bash
dotnet test
```

CI (GitHub Actions, [build.yml](.github/workflows/build.yml)) builds the web UI
first, then runs the suite with `-p:SkipSpaBuild=true` on every push/PR; a known
Testcontainers flake is retried once before failing. Suite baseline: **191/191 green**.

## Repository layout

C# split into class libraries by system function, one host project (three-mode):

```
src/AgentContext.Domain/        entities + enums (no dependencies beyond pgvector types)
src/AgentContext.Infrastructure/ EF Core DbContext + migrations + design-time factory
src/AgentContext.Application/   application services (primary test seam) + AddApplicationServices
                                + localization resources (embedded i18n JSON)
src/AgentContext.Host/          three-mode entrypoint: Program (--web / --mcp-stdio / --apphost),
                                Controllers/ (REST), Mcp/ (stdio server + tools),
                                Workers/ (session processing, knowledge hygiene),
                                Observability/ (OTel), AppHost/ (Aspire), wwwroot (built UI)
web/                            React UI (Vite + TS + shadcn/ui + react-i18next + shiki),
                                built into the host's wwwroot
i18n/                           single-store localization (ADR 0008): en-US.json, zh-CN.json
skills/                         default skill-package root for local dev (Skills__Directory)
docs/                           spec, ADRs, guides, design exploration, validation records
tests/AgentContext.Tests/       seam tests (application services vs Testcontainers pgvector)
                                + adapter smoke tests (REST / MCP stdio / OTel / web host)
AgentContext.slnx               solution (new XML format)
Dockerfile · docker-compose.yml portal + Postgres(pgvector) + Aspire dashboard
.github/workflows/              build.yml (test) · release.yml (GHCR image + GitHub Release)
```

Versions are centralised in `Directory.Packages.props` (CPM, transitive pinning on).
