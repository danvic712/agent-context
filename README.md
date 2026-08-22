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
- **Observability** — OpenTelemetry logs + traces + metrics exported to the in-app [Aspire dashboard](http://localhost:8080/monitor/resources) by default; `service.name=agent-context`.

## Quick start

```bash
docker compose up -d --build
```

This starts everything with no manual steps:

| Service | Address | Notes |
|---|---|---|
| Agent Context image | http://localhost:8080 | AppHost container with portal UI + REST API + Streamable HTTP MCP at `/mcp`; applies EF Core migrations at startup |
| Aspire dashboard | http://localhost:8080/monitor/resources | In-process dashboard with Resources, logs, traces and metrics; raw :18888 stays container-internal |
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
It starts the complete environment as an Aspire DistributedApplication:

- portal child process — REST API (`/api/*`), React UI, health at `/api/health`, and Streamable HTTP MCP at `/mcp`;
- in-process Aspire dashboard with the Resources view;
- pgvector Postgres when no external `ConnectionStrings__Default` is supplied.

In Docker Compose, Postgres is external to the image and is passed through
`ConnectionStrings__Default`; the image still starts the portal and dashboard.
`HOST_MODE=portal` is an internal child-process marker, not a user-facing mode.
See [`docs/guides/apphost-mode.md`](docs/guides/apphost-mode.md).

All processes share one DI graph (`AddApplicationServices`).

```
Users (React UI / Craft Agents)
          │
          ▼
┌──────────────────────────────────────────┐
│ AgentContext image / AppHost             │
│  portal: UI + REST + MCP /mcp (:8080)   │
│  dashboard: Resources + OTel (:18888)   │
└──────────────┬───────────────────────────┘
               │ SQL
               ▼
        PostgreSQL (+ pgvector)
```

## Development

```bash
# Full environment: portal + dashboard + local pgvector Postgres
dotnet run --project src/AgentContext.Host

# UI dev server with /api, /monitor (including websockets), and /navfix.js proxied to http://localhost:8080
cd web && npm run dev
```

### Docker build notes

The [Dockerfile](Dockerfile) uses three stages: Node builds the React UI,
.NET publishes the Host, and the `aspnet` runtime image runs the AppHost.
BuildKit caches npm packages and target-architecture NuGet packages. Aspire's
DCP and Dashboard RID packages are staged into the runtime NuGet cache because
they are tooling packages omitted from `deps.json`; the temporary staging area
is kept outside `/app` so the final image does not contain a duplicate copy.

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
src/AgentContext.Host/          single no-args entrypoint: Program (AppHost + portal child),
                                Controllers/ (REST), Mcp/ (HTTP tools),
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
Dockerfile · docker-compose.yml AppHost image + Postgres(pgvector) + in-process Aspire dashboard
.github/workflows/              build.yml (test) · release.yml (GHCR image + GitHub Release)
```

Versions are centralised in `Directory.Packages.props` (CPM, transitive pinning on).
