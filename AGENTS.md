# AGENTS.md

A shared context layer for AI agents — manage skills, memory, sessions, and knowledge across personal, family, and team workspaces. Self-hosted, .NET + React, MCP-first.

- **Stack**: .NET 10 / ASP.NET Core / EF Core / Serilog on the backend; React 19 + Vite + TS + shadcn/ui + react-i18next + shiki on the frontend; PostgreSQL + pgvector.
- **One public entrypoint** ([ADR 0006](docs/adr/0006-single-project-dual-mode.md)): running with no arguments starts the ASP.NET Core host with REST API, React UI, and Streamable HTTP MCP at `/mcp`. PostgreSQL is supplied by Docker Compose or an external connection string. All features share one DI graph.
- **Features**: Learning Engine (session → knowledge pipeline, T3) · retrieval with conflict groups (T4) · knowledge hygiene / review / archive (T5, T8) · versioned **skill packages** on the filesystem (T6, T12) · source-aware Usage token ledger (Analytics surface deferred for redesign) · LLM endpoint + language + theme settings in DB (T10, T11, T12) · optional OpenTelemetry logs/traces/metrics over OTLP (T13).
- **Latest milestones**: T11 localization, T12 product-grade UI + Skill package model, T13 optional OpenTelemetry, and T14 Streamable HTTP MCP + single-entrypoint startup (2026-08-18).

## MCP surface

The platform exposes five v1 tools over Streamable HTTP at `/mcp` (see the MCP registrations in `src/AgentContext.Host/Mcp/`):

| Tool | Purpose |
|---|---|
| `save_session` | report a session (structured summary + model/tokens + optional `remember`) |
| `search_memory` | domain-scoped, cosine-ranked knowledge retrieval |
| `find_similar_solution` | find past solutions to a problem |
| `get_skill` | load a skill package (manifest + per-file content) |
| `rate_knowledge` | useful / not-useful feedback (feeds Confidence) |

Resources: `skill://{domain}/{slug}/{file}` (sub-paths support `%2F`).

Register the binary as a local Craft Agents stdio source: see [`docs/guides/craft-agents-source.md`](docs/guides/craft-agents-source.md). The in-repo guide skill lives at [`docs/skills/craft-agents-guide.md`](docs/skills/craft-agents-guide.md).

## Agent skills

### Issue tracker

GitHub Issues, driven through the `gh` CLI (infer repo from `git remote`). See `docs/agents/issue-tracker.md`.

### Triage labels

Default five: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: root `CONTEXT.md` + `docs/adr/`. Read them before exploring. See `docs/agents/domain.md`.

### Coding standards

Root `CODING_STANDARDS.md` documents the repo's C# conventions (notably: interfaces and shared DTOs live in `Contracts` folders, implementations in feature folders).

## Documentation map

| Path | What it is |
|---|---|
| [`docs/spec.md`](docs/spec.md) | authoritative platform spec (capabilities, ADR table, testing strategy) |
| [`CONTEXT.md`](CONTEXT.md) | canonical terminology (Workspace, Domain, Agent, Session, Skill, Knowledge, Confidence, …) |
| [`docs/adr/`](docs/adr/) | architecture decision records 0001–0008 |
| [`docs/guides/`](docs/guides/) | operational guides (deployment, Craft Agents source) |
| [`docs/validation/`](docs/validation/) | per-ticket UI/end-to-end validation write-ups |
| [`docs/design/`](docs/design/) | UI design exploration HTML files (botanical theme lineage) |
| [`docs/handoffs/`](docs/handoffs/) | context bridges for implementers |

## Development & testing

- **Build**: `dotnet build` (SPA target rebuilds the React UI into `wwwroot`). Skip with `-p:SkipSpaBuild=true` after `cd web && npm ci && npm run build`. Docker builds use BuildKit caches for npm and NuGet packages.
- **Test**: `dotnet test` — needs Docker (Testcontainers spins up a real pgvector Postgres). Two seams: application-service seam tests + thin adapter tests (REST/MCP HTTP via the host, plus OTel and web-host smoke tests). Baseline **191/191 green**.
- **CI**: [`.github/workflows/build.yml`](.github/workflows/build.yml) runs on every push/PR (web build → dotnet build/test, one retry for Testcontainers flakiness); [`.github/workflows/release.yml`](.github/workflows/release.yml) builds the multi-arch GHCR image + GitHub Release on `v*` tags.
- **Local stack**: `docker compose up -d` → portal :8080 and external Postgres :5432. Use `docker compose up -d --build` to rebuild the image.

## Engineering gotchas

- `.gitignore` anchors `/skills/` (macOS `core.ignoreCase` would otherwise shadow `src/**/Skills/` source files) — never widen it back to a bare `skills/` glob, and use `git add -f` if a real skill needs force-adding.
- OpenTelemetry export is opt-in: set `OTEL_EXPORTER_OTLP_ENDPOINT` (and optionally `OTEL_EXPORTER_OTLP_PROTOCOL`) to send logs, traces, and metrics to an OTLP collector. `OTEL_SDK_DISABLED=true` disables it explicitly.
- Localization lives in grouped JSON resources at the repo root (`locales/{locale}/*.json`, ADR 0008) — embedded by the backend, imported by the Vite frontend (needs `server.fs.allow: ['..']`).
- The MCP stdio subprocess is single-session; logs go to stderr to keep the JSON-RPC stream clean.
