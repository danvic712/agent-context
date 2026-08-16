# Agent Context — Platform Spec

> **Current state (2026-08-16)**: T1–T10 delivered and closed (#2–#11); T11–T13 planned (#12–#14). Terminology: `CONTEXT.md` · Decisions: `docs/adr/0001–0008` · Context bridge: `docs/handoffs/t1-t10-delivered.md` · Research: `docs/research/competitive-landscape.md`.

## 1. Positioning & Scope

Agent Context is a self-hosted shared context layer for AI agents: agents report Sessions over MCP, the Learning Engine distills them into domain-scoped Knowledge with Confidence, future Sessions retrieve that Knowledge, Skills are managed centrally, and the UI reports Usage.

- **Users**: personal & family first (a technically capable person does the initial setup), small teams next. Domain-agnostic — dev, home, business, any domain.
- **Four value axes**: Shared AI Context (foundation) · Continuous Learning (foundation, table stakes) · AI Capability Management (core) · AI Usage Intelligence (**differentiator**).
- **Deployment**: self-hosted Docker Compose first (ADR 0002); SaaS later. One .NET binary, dual-mode (ADR 0006).

## 2. Delivered Capabilities (T1–T10)

### 2.1 Integration — MCP gateway (T2, T6, T9)
- One project, dual-mode: `--web` = REST API + React UI + MCP-over-HTTP; `--mcp-stdio` = MCP server for Craft Agents (ADR 0006).
- Five tools: `save_session`, `search_memory`, `find_similar_solution`, `get_skill`, `rate_knowledge`. Resources: `skill://{domain}/{slug}`, `knowledge://{id}`.
- Craft Agents integration: registered as a local stdio source + in-repo guide skill (`docs/skills/craft-agents-guide.md`, `docs/guides/craft-agents-source.md`); full-loop validated with real LLM usage (`docs/validation/t9-full-loop.md`).

### 2.2 Sessions & Usage (T2)
- `save_session`: structured summary (task/conclusion/key snippets) + explicit domain tag + optional `remember` (full context) + model/tokens/cost. Sessions land `Pending` in Postgres-as-queue (ADR 0005).
- `Usage` records per session; cost computed from token counts × maintained model pricing table (T7).

### 2.3 Learning Engine (T3)
- BackgroundService polls pending Sessions → pipeline: dedup → LLM extraction (OpenAI-compatible endpoint from the settings table, ADR 0003) → Knowledge (`Problem`/`Solution`/`Pattern` + Confidence) → embedding (`vector(1536)`) → pgvector.
- Confidence (initial): 0.4×self-assessment + 0.2×field-completeness, cap 0.6. Conflict detection: similarity ≥0.9 corroborates, [0.6,0.9) forms/shared conflict groups; retrieval shows both sides.
- Retries: `NextAttemptAtUtc = now + 30s×2^n` (cap 1h, budget 5); exhausted budget stays `Failed`, visible, never deleted. **Pipeline idles (never fails Sessions) while the LLM endpoint is unconfigured.**

### 2.4 Retrieval (T4)
- `search_memory(domain, query)` / `find_similar_solution(domain, problem)` — domain-scoped, cosine-ranked, Confidence ≥ 0.5 threshold, Top 10, conflict-group partners appended side by side. REST + MCP.

### 2.5 Knowledge management (T5, T8)
- List with Confidence + source Session; review list with threshold; archive; private marker; delete; `rate_knowledge` (useful → +0.1 cap 1.0; not-useful → cleared to review).
- Hygiene: temporal decay for stale items; low-confidence / long-unused → Review → Archived; restore from archive; scheduled cleanup + manual trigger; engine health view (queued/processing/failed/retry-scheduled).

### 2.6 Skills (T6)
- Versioned per `(domain, slug)`; publish bumps version, history retained; `get_skill` returns the latest; `skill://{domain}/{slug}` resource. CRUD + UI.

### 2.7 Analytics (T7)
- Session overview: sessions / tokens / cost by workspace / domain / agent; maintained model pricing table; report page.

### 2.8 LLM endpoint configuration (T10)
- Settings stored in the `settings` table (DB, per-call resolution — no restart). `GET/PUT /api/settings/llm-options` (API key masked); first-run wizard step (skippable, Learning Engine idles until configured); settings page.

### 2.9 Ops
- Auto-creates the database + applies migrations at startup; Docker Compose = portal + postgres(pgvector); 140/140 tests green (seam + adapter suites).

## 3. Planned (T11–T13)

- **T11 — Platform localization (#12)**: platform-level language `en-US`/`zh-CN` stored in the settings table; single JSON resource store shared by frontend and backend (`i18n/{locale}.json`, namespaces `ui` + `errors`, ADR 0008 — no .resx, one file per locale); react-i18next across all components; first-run wizard language step + settings dropdown; backend coded errors → localized `message` + stable `errorCode` (REST + MCP); extraction prompt outputs the configured language while preserving identifiers/technical terms/original snippets; existing Knowledge untouched.
- **T12 — Product-grade UI + theme + Skill package (#13)**: Direction D product UI (ref. `docs/design/ui-direction-d.html` — near-black + indigo, Instrument Sans + JetBrains Mono, dark/light); color theme selector (Light/Dark/System, persisted); Skill evolves from single markdown to a **filesystem-backed package** (file manifest + `SKILL.md` + code/assets), UI file tree / rendered markdown (react-markdown + shiki) / code editing / uploads; `get_skill` returns the manifest, resources extend to `skill://{domain}/{slug}/{file}`; existing Skills migrate `Instructions` → `SKILL.md`. Components adopt `t('key')` seams from the start (coordinates with T11).
- **T13 — OpenTelemetry + Aspire dashboard (#14)**: backend exports logs (Serilog.Sinks.OpenTelemetry dual-write) + traces + metrics, **enabled by default**; OTLP endpoint from `OTEL_EXPORTER_OTLP_ENDPOINT`, default `http://aspire-dashboard:4317` (compose) / `localhost:4317` (standalone); new compose service `aspire-dashboard` (UI 18888, OTLP 4317/4318); `service.name=agent-context`; `OTEL_SDK_DISABLED` as escape hatch.

## 4. Architecture

```
               Users (React UI, Direction D, dark/light themes)
                            |
        ┌───────────────────┴───────────────────┐
        │   AgentContext (single .NET project)  │
        │   --web: REST API + UI + MCP-over-HTTP│
        │   --mcp-stdio: MCP for Craft Agents   │
        │   shared: EF Core / retrieval /       │
        │   learning / BackgroundService        │
        │   OTel exporter (T13) → aspire-dash   │
        └───────────────────┬───────────────────┘
                            |
      PostgreSQL (+ pgvector)   ·   Aspire dashboard (T13)
              (no Redis, no Hangfire)
```

- Single project, dual-mode entrypoint (ADR 0006); one DI graph, one DbContext, one config; feature folders + `AppService` convention (CODING_STANDARDS.md).
- Background processing: `BackgroundService` + Postgres-as-queue (ADR 0005); hygiene on `PeriodicTimer`.
- Platform settings live in the `settings` table (ADR 0003); localization resources in one JSON store (ADR 0008).

## 5. Tech Stack

- **Backend**: .NET 10, ASP.NET Core, EF Core, Serilog, Microsoft Agent Framework (chat + embeddings), OpenTelemetry (T13).
- **Frontend**: React + TypeScript + shadcn/ui (Direction D refactor), react-i18next (T11), react-markdown + shiki (T12).
- **Data**: PostgreSQL + pgvector; Redis deferred (ADR 0007); Hangfire deferred (ADR 0005).

## 6. Data Model

```
Workspace ──┬── Domain ──┬── Knowledge (Type/Content/Confidence/Embedding/ConflictGroupId/Status/IsPrivate)
            │            └── Skill (versioned; package files on filesystem in T12)
            ├── Membership ── User
            └── Session ──┬── Agent
                          ├── Usage (tokens/cost by model)
                          └── ⟶ Knowledge (distilled into)
AppSetting (platform settings: LLM endpoint, language) · ModelPricing (cost table)
```

## 7. Workspace & Visibility

- Personal and Family today; Team planned. Domains live inside workspaces; cross-workspace knowledge flow is explicit only.
- Visibility: domain-level shared/private + per-item private marker. Auth: first-run admin + personal Workspace; invite-link membership — approach flagged pending in the design interview.

## 8. Key Decisions (ADRs)

| # | Decision |
|---|---|
| 0001 | Sessions reported by agents over MCP; no traffic proxy |
| 0002 | Self-hosted Docker Compose first; SaaS later |
| 0003 | Platform LLM via configurable OpenAI-compatible endpoint (settings table) |
| 0004 | MVP scope = learning loop + thin skills + session overview; explicit no-list |
| 0005 | BackgroundService + Postgres-as-queue; Hangfire deferred |
| 0006 | One project, dual-mode entrypoint |
| 0007 | Redis deferred from MVP stack |
| 0008 | Localization resources in one JSON file per locale, shared frontend/backend |

## 9. Testing

- **Primary seam**: application services against Testcontainers pgvector (real DB; LLM faked via `FakeLlmClient`/`FakeSettingsAppService`).
- **Adapter seam**: thin REST (`WebApplicationFactory`, run setup first) + MCP (real stdio process, in-process client) contract tests.
- Baseline 140/140 green, zero functional regressions; OTel stays green (T13 acceptance).

## 10. Out of Scope (no-list)

Skill marketplace · enterprise SSO/audit · auto memory injection · git-synced skills · per-item ACL · built-in chat UI · traffic proxy · SaaS hosting · Redis · Hangfire · `npx install`-style remote skill install (post-T12) · knowledge translation migration (post-T11).

## 11. References

- `docs/handoffs/t1-t10-delivered.md` (context bridge for implementers)
- `docs/design/ui-direction-d.html` (T12 design baseline)
- `docs/guides/craft-agents-source.md` · `docs/skills/craft-agents-guide.md` · `docs/validation/t9-full-loop.md`
- `docs/research/competitive-landscape.md` (positioning evidence)
