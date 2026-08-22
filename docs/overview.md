# Agent Context - Project Overview

> Revised after the design interview (2026-08). Decisions are recorded as ADRs in `docs/adr/`; terminology lives in `CONTEXT.md`.

## 1. Introduction

Agent Context is an AI agent context management platform — a shared intelligence layer for AI agents.

Modern AI agents are powerful, but their knowledge and capabilities are fragmented:

- Skills are installed separately on different machines.
- Prompts and workflows are difficult to maintain and share.
- AI conversations are lost after each session.
- Previous solutions and experiences cannot be reused efficiently.
- There is limited visibility into AI usage, token consumption, and effectiveness.

Agent Context solves this with a centralized, self-hostable layer where agents share skills, memory, knowledge, and session history.

**Scope**: Personal and family workspaces come first (initial setup requires a technically capable person); small teams follow. The platform is **domain-agnostic** — dev, home, business, or any domain of life or work.

## 2. Goals & Positioning

Four value axes, all supported — with a clear hierarchy:

| Axis | Role |
|---|---|
| Shared AI Context — cross-agent unified context via MCP | Foundation |
| Continuous Learning — sessions become reusable knowledge | Foundation (table stakes; must be solid) |
| AI Capability Management — skills, versioned and domain-scoped | Core feature |
| AI Usage Intelligence — sessions, tokens, effectiveness | **Differentiator** |

Market research ([competitive-landscape.md](./research/competitive-landscape.md)) shows session→knowledge automation is now table stakes across the field; **usage intelligence, governance, and cross-platform unified context remain open territory**. The learning loop is the ground everything stands on; the wedge is governed, cross-agent context with usage intelligence on top.

## 3. Key Decisions

| # | Decision | ADR |
|---|---|---|
| Ingestion | Sessions reported by agents over MCP (push); **no traffic proxy** | [0001](./adr/0001-session-ingestion-via-mcp-push.md) |
| Deployment | Self-hosted Docker Compose first; SaaS later | [0002](./adr/0002-self-hosted-first-deployment.md) |
| Platform inference | Configurable OpenAI-compatible providers with independent Chat/Embedding routes | [0009](./adr/0009-platform-inference-configuration.md) |
| MVP scope | Learning loop + thin skills + session overview; explicit no-list | [0004](./adr/0004-mvp-scope-and-no-list.md) |
| Background | BackgroundService + Postgres-as-queue; Hangfire deferred | [0005](./adr/0005-backgroundservice-over-hangfire-for-mvp.md) |
| Hosting | One project, one entrypoint: no-args startup runs the full 3-in-1 environment (portal UI/REST/MCP + Aspire dashboard + postgres) | [0006](./adr/0006-single-project-dual-mode.md) |
| Cache | Redis deferred from MVP stack | [0007](./adr/0007-redis-deferred-from-mvp.md) |

## 4. High Level Architecture

One project, one entrypoint ([ADR 0006](./adr/0006-single-project-dual-mode.md)) —
running the binary with no arguments starts everything:

```
                 Users (React UI, botanical theme)
                      |
        ┌─────────────┴─────────────┐
        │   AgentContext (single .NET project)   │
        │   default: Aspire DistributedApp       │
        │     ├─ portal: REST API + UI + MCP /mcp│
        │     └─ in-process dashboard            │
        │   shared: EF Core / retrieval /        │
        │   learning / BackgroundService         │
        │   OTel exporter → dashboard            │
        └───────┬──────────────────┬─────────────┘
                |                  |
      PostgreSQL (+ pgvector)  Aspire dashboard
      (Redis deferred, ADR 0007)
                |
         Craft Agents (Streamable HTTP /mcp)
```

## 5. Technology Stack

- **Backend**: ASP.NET Core (.NET 10), Entity Framework Core, Serilog, Microsoft Agent Framework (chat + embeddings); OpenTelemetry (traces/metrics via OTel SDK, logs via Serilog sink); Aspire (AppHost mode); background processing via `BackgroundService` + Postgres-as-queue (Hangfire deferred — [ADR 0005](./adr/0005-backgroundservice-over-hangfire-for-mvp.md))
- **Frontend**: React, TypeScript, shadcn/ui (botanical theme, Tailwind v4), react-i18next, react-markdown + shiki
- **Data**: PostgreSQL + pgvector (Redis deferred — [ADR 0007](./adr/0007-redis-deferred-from-mvp.md))
- **CI/CD**: GitHub Actions — `build.yml` (build + test on push/PR), `release.yml` (v* tags → multi-arch GHCR image + GitHub Release)
- **Integration**: one project, one mode — no-args startup runs the full
  environment; the portal serves REST/UI + the MCP toolset over Streamable HTTP
  at `/mcp` (Craft Agents connect by URL).

## 6. Core Components

### 6.1 MCP Gateway (v1 toolset)

The MCP surface lives in the same project as the API ([ADR 0006](./adr/0006-single-project-dual-mode.md)): the portal serves the v1 toolset over Streamable HTTP at `/mcp` — Craft Agents connect by URL.

Tools:
- `save_session` — domain, structured summary, optional `remember` (full context), optional pre-structured knowledge
- `search_memory` / `find_similar_solution` — shared retrieval backend in v1
- `get_skill` — fetch a skill by slug
- `rate_knowledge` — useful/useless feedback (feeds Confidence)

Resources: `skill://{domain}/{slug}/{file}` (T12 package files), `knowledge://{id}`

### 6.2 Session Management

- Agents report a **structured summary** (task, conclusion, key snippets) plus an optional Usage payload containing the reported model snapshot, input tokens, cached input tokens, and output tokens.
- Full original context is stored only when the user explicitly says "remember" (which also marks it important).
- `Usage` is a source-aware token ledger: reported Session rows attach to a Session,
  while Learning Engine rows may be sessionless and may carry nullable route/capability
  bindings. Analytics exposes token counts only; cost is not persisted on Usage or
  included in the Analytics contracts.

### 6.3 Learning Engine

Pipeline: `save_session` → background worker → dedup → extraction LLM → knowledge item (`Problem` / `Solution` / `Pattern` + confidence) → conflict detection → embedding → pgvector.

**Confidence** = extraction self-assessment (40%) + field completeness (20%) + cross-session corroboration (20%) + novelty/dedup (20%), then adjusted by usage feedback (citation +0.1 capped; "useless" clears; temporal decay).

**Hygiene**: decay for stale items + scheduled cleanup (low-confidence / long-unused → review or archive) + conflict pairs shown side by side at retrieval.

### 6.4 Skill Management (packages)

- A skill = a **filesystem package** (manifest + `SKILL.md` + assets/scripts), versioned per `(domain, slug)`.
- Retrieved via `get_skill` (manifest + per-file content); UI renders a file tree, edits files, drag-drops uploads and imports zips; publish bumps the version, history retained.
- Legacy `Instructions` column is lazily migrated to `SKILL.md`; git sync and marketplace are later.

### 6.5 Analytics (MVP)

- Session overview: sessions and token counts — by workspace / domain / agent.
- Data lands automatically with `save_session`; no extra collection.

## 7. Data Model

```
Workspace ──┬── Domain ──┬── Knowledge (confidence, source session)
            │            └── Skill (versioned)
            ├── Membership ── User
            └── Session ──┬── Agent
                          ├── Usage (source-aware tokens by model)
                          └── ⟶ Knowledge (distilled into)
```

## 8. Workspace Model

- **Personal** and **Family** now; **Team** planned.
- **Domains live inside workspaces**; cross-workspace knowledge flow is always explicit.
- **Visibility**: domain-level shared/private + per-item private marker.
- **Onboarding**: first-run wizard creates admin + personal workspace; members join via invite link *(auth approach flagged pending)*.

## 9. MVP Scope

**In**: learning loop (session → knowledge → retrieval), skill packages (file-tree management + publish), session overview, platform localization, inference/theme/language settings, OTel observability + Aspire dashboard, Craft Agents integration (MCP server + guide skill), one personal workspace, domain-level visibility.

**Out (no-list)**: skill marketplace, enterprise SSO/audit, auto memory injection, git-synced skills, per-item ACL, built-in chat UI, traffic proxy, SaaS hosting.

## 10. Future Features

- **Agent Marketplace** — discover and share skills, workflows, agents
- **Enterprise** — SSO, RBAC, audit logs, usage governance
- **AI Optimization** — repeated-prompt detection, missing-skill suggestions, knowledge-gap analysis
- **Advanced memory** — auto injection, git-synced skills, richer analytics

## 11. Project Principles

- **Centralized Context** — agents should not start from zero every session.
- **Learn Once, Reuse Everywhere** — knowledge from one session benefits future sessions.
- **Agent Independent** — support different agents via open standards (MCP).
- **Human Controlled** — users control what knowledge is shared and how it is used (confidence, visibility, and hygiene are the enforcement mechanisms).
