# Agent Context

A shared context layer for AI agents — manage skills, memory, sessions, and knowledge across personal, family, and team workspaces. Self-hosted, .NET + React, MCP-first.

> Canonical design docs: [`docs/spec.md`](docs/spec.md) · Terminology: [`CONTEXT.md`](CONTEXT.md) · Decisions: [`docs/adr/`](docs/adr/) · Validation history: [`docs/validation/`](docs/validation/)

## Features

- **MCP gateway** — five v1 tools over Streamable HTTP at `/mcp` for Craft Agents (`save_session`, `search_memory`, `find_similar_solution`, `get_skill`, `rate_knowledge`) + `skill://{domain}/{slug}/{file}` resources.
- **Learning Engine** — a background pipeline turns agent session summaries into domain-scoped, confidence-scored Knowledge (Problem / Solution / Pattern) via a configurable OpenAI-compatible LLM endpoint; embeddings land in pgvector.
- **Retrieval** — domain-scoped, cosine-ranked search with a Confidence threshold; conflict groups surfaced side by side.
- **Knowledge management** — review / archive / restore, temporal decay hygiene, private markers, usage feedback (`rate_knowledge`).
- **Skills** — versioned per `(domain, slug)`, stored as filesystem **packages** (file tree + `SKILL.md` + assets) with an in-browser file editor, drag-drop uploads and zip import.
- **Usage recording** — source-aware session and Learning Engine token ledger; the Analytics UI/API is deferred for redesign.
- **Settings** — LLM endpoint, platform language (`en-US` / `zh-CN`), and color theme (`light` / `dark` / `system`), all persisted in the DB and applied without restart.
- **Localization** — full platform UI + backend errors in the configured language, one JSON store per locale (ADR 0008).
- **Observability** — optional OpenTelemetry logs + traces + metrics exported to any OTLP collector; `service.name=agent-context`.

## Quick start

```bash
docker compose up -d --build
```

This starts everything with no manual steps:

| Service | Address | Notes |
|---|---|---|
| Agent Context image | http://localhost:8080 | ASP.NET Core host with UI + REST API + Streamable HTTP MCP at `/mcp`; applies EF Core migrations at startup |
| Postgres (pgvector) | localhost:5432 | `agent_context` / `agent_context`; external to the application image |

Open http://localhost:8080 and the **first-run wizard** creates your admin account
and a Personal Workspace (language → account → optional LLM endpoint). Rerunning
the wizard is blocked once configured.

Prebuilt images (`ghcr.io/danvic712/agent-context:latest`, multi-arch
linux/amd64 + arm64) are published by the [release workflow](.github/workflows/release.yml)
on every `v*` tag; `docker compose up -d` pulls them automatically. The local
`build:` block is kept for source builds. Add `--build` when validating Dockerfile
changes locally.

## Architecture

One .NET project has one public startup contract: **run with no arguments**.
It starts the ASP.NET Core host directly:

- REST API (`/api/*`), React UI, health at `/api/health`, and Streamable HTTP MCP at `/mcp`;
- PostgreSQL supplied through `ConnectionStrings__Default`.

Docker Compose starts PostgreSQL beside the application and passes it through
`ConnectionStrings__Default`.

All processes share one DI graph (`AddApplicationServices`).

```
Users (React UI / Craft Agents)
          │
          ▼
┌──────────────────────────────────────────┐
│ AgentContext ASP.NET Core host            │
│  UI + REST + MCP /mcp (:8080)             │
│  optional OTLP export                     │
└──────────────┬───────────────────────────┘
               │ SQL
               ▼
        PostgreSQL (+ pgvector)
```

## Development

```bash
# Full environment: portal + local pgvector Postgres
dotnet run --project src/AgentContext.Host

# UI dev server with /api proxied to http://localhost:8080
cd web && npm run dev
```

### Docker build notes

The [Dockerfile](Dockerfile) uses three stages: Node builds the React UI,
.NET publishes the Host, and the `aspnet` runtime image runs the Host directly.
BuildKit caches npm and NuGet packages.

Tests run against a real Postgres with pgvector via Testcontainers (Docker required):

```bash
dotnet test
```

CI (GitHub Actions, [build.yml](.github/workflows/build.yml)) builds the web UI
first, then runs the suite with `-p:SkipSpaBuild=true` on every push/PR; a known
Testcontainers flake is retried once before failing. Suite baseline: **191/191 green**.

## Repository layout

C# split into class libraries by system function, one host project (single public entrypoint):

```
src/AgentContext.Domain/        entities + enums (no dependencies beyond pgvector types)
src/AgentContext.Infrastructure/ EF Core DbContext + migrations + design-time factory
src/AgentContext.Application/   application services (primary test seam) + AddApplicationServices
                                + localization resources (embedded i18n JSON)
src/AgentContext.Host/          single no-args entrypoint: Program (ASP.NET Core host),
                                Controllers/ (REST), Mcp/ (HTTP tools),
                                Workers/ (session processing, knowledge hygiene),
                                Observability/ (OTel), wwwroot (built UI)
web/                            React UI (Vite + TS + shadcn/ui + react-i18next + shiki),
                                built into the host's wwwroot
i18n/                           single-store localization (ADR 0008): en-US.json, zh-CN.json
skills/                         default skill-package root for local dev (Skills__Directory)
docs/                           spec, ADRs, guides, design exploration, validation records
tests/AgentContext.Tests/       seam tests (application services vs Testcontainers pgvector)
                                + adapter smoke tests (REST / MCP stdio / OTel / web host)
AgentContext.slnx               solution (new XML format)
Dockerfile · docker-compose.yml ASP.NET Core image + Postgres(pgvector)
.github/workflows/              build.yml (test) · release.yml (GHCR image + GitHub Release)
```

Versions are centralised in `Directory.Packages.props` (CPM, transitive pinning on).
