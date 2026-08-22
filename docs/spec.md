# Agent Context — Platform Spec

> **Current state (2026-08-18)**: T1–T14 are delivered; T15's complete-image and same-origin dashboard implementation is present locally, with final Docker/full-loop validation pending. Terminology: `CONTEXT.md` · Decisions: `docs/adr/0001–0008` · Context bridge: `docs/handoffs/t1-t10-delivered.md` · Validation records: `docs/validation/` · Research: `docs/research/competitive-landscape.md`.

## 1. Positioning & Scope

Agent Context is a self-hosted shared context layer for AI agents: agents report Sessions over MCP, the Learning Engine distills them into domain-scoped Knowledge with Confidence, future Sessions retrieve that Knowledge, Skills are managed centrally, and the UI reports Usage.

- **Users**: personal & family first (a technically capable person does the initial setup), small teams next. Domain-agnostic — dev, home, business, any domain.
- **Four value axes**: Shared AI Context (foundation) · Continuous Learning (foundation, table stakes) · AI Capability Management (core) · AI Usage Intelligence (**differentiator**).
- **Deployment**: self-hosted Docker Compose first (ADR 0002); SaaS later. One .NET binary, one entrypoint (ADR 0006).

## 2. Delivered Capabilities (T1–T15)

### 2.1 Integration — MCP gateway (T2, T6, T9)
- One project, one entrypoint: no-args startup runs the full environment — portal (REST API + React UI + MCP over Streamable HTTP at `/mcp`) + Aspire dashboard + postgres, as one DistributedApplication (ADR 0006). Craft Agents connect to the MCP toolset by URL; the legacy stdio server remains as an internal/test path.
- Five tools: `save_session`, `search_memory`, `find_similar_solution`, `get_skill`, `rate_knowledge`. Resources: `skill://{domain}/{slug}/{file}` (T12), `knowledge://{id}`.
- Craft Agents integration: registered as a local stdio source + in-repo guide skill (`docs/skills/craft-agents-guide.md`, `docs/guides/craft-agents-source.md`); full-loop validated with real LLM usage (`docs/validation/t9-full-loop.md`).

### 2.2 Sessions & Usage (T2)
- `save_session`: structured summary (task/conclusion/key snippets) + explicit domain tag + optional `remember` (full context) + optional reported Usage payload (`model`, `inputTokens`, `cachedInputTokens`, `outputTokens`). Sessions land `Pending` in Postgres-as-queue (ADR 0005). Reported model snapshots are stored as supplied and are independent of platform InferenceRoute configuration.
- `Usage` is a source-aware token ledger: `reported_session` rows attach to a Session, while `learning_engine` rows may be sessionless and may carry nullable route/capability bindings. Cached input tokens are a subset of input tokens. Analytics exposes token counts only; cost is not part of the Usage or Analytics contracts.

### 2.3 Learning Engine (T3)
- BackgroundService polls pending Sessions → pipeline: dedup → chat extraction through the configured inference route → Knowledge (`Problem`/`Solution`/`Pattern` + Confidence) → embedding through the configured embedding route (`vector(1536)`) → pgvector.
- Confidence (initial): 0.4×self-assessment + 0.2×field-completeness, cap 0.6. Conflict detection: similarity ≥0.9 corroborates, [0.6,0.9) forms/shared conflict groups; retrieval shows both sides.
- Retries: `NextAttemptAtUtc = now + 30s×2^n` (cap 1h, budget 5); exhausted budget stays `Failed`, visible, never deleted. **Pipeline idles (never fails Sessions) while the inference routes are unconfigured.**
- Extraction output follows the **configured platform language** (T11), preserving identifiers / technical terms / original snippets.

### 2.4 Retrieval (T4)
- `search_memory(domain, query)` / `find_similar_solution(domain, problem)` — domain-scoped, cosine-ranked, Confidence ≥ 0.5 threshold, Top 10, conflict-group partners appended side by side. REST + MCP.

### 2.5 Knowledge management (T5, T8)
- List with Confidence + source Session; review list with threshold; archive; private marker; delete; `rate_knowledge` (useful → +0.1 cap 1.0; not-useful → cleared to review).
- Hygiene: temporal decay for stale items; low-confidence / long-unused → Review → Archived; restore from archive; scheduled cleanup + manual trigger; engine health view (queued/processing/failed/retry-scheduled).

### 2.6 Skills (T6, T12)
- Versioned per `(domain, slug)`; publish bumps version, history retained; `get_skill` returns the latest; `skill://{domain}/{slug}/{file}` resource. CRUD + UI.
- **Skill package model (T12)**: each skill is a filesystem package (manifest + `SKILL.md` + assets/scripts), stored under `Skills__Directory` (data volume in compose); in-browser file tree / markdown rendering (react-markdown + shiki) / code editing / drag-drop uploads / zip import; legacy `Instructions` column lazily migrated to `SKILL.md`.

### 2.7 Analytics (T7)
- Session overview: sessions and token counts by workspace / domain / agent; report page.

### 2.8 Inference configuration (T14, issue #16)
- Platform-level inference configuration is stored across three PostgreSQL tables: `inference_configurations` (configuration identity and timestamps), `inference_routes` (one Chat and one Embedding binding), and `inference_providers` (reusable OpenAI-compatible connection data and protected API-key secret material). `inference_providers` has no reverse configuration foreign key.
- Chat and Embedding routes may use different providers and models. The MVP supports OpenAI-compatible Chat Completions and Embeddings only; embedding validation and runtime require `1536` dimensions.
- REST endpoints are outside Settings: `GET/PUT /api/inference/configuration` and `POST /api/inference/configuration/verify`. Verification probes the unsaved draft and has no session, usage, or persistence side effects; save/create requires both route checks to pass.
- Settings and first-run Setup share the same contract. Setup has exactly three steps: Account & preferences, Model service, Review & create. Account, preferences, workspace, providers and routes are committed atomically.
- API keys are write-only at the REST boundary: reads return only configured/masked state. Changes apply immediately without a restart; the platform scope remains global in this MVP.

### 2.9 Localization (T11)
- Platform-level language `en-US` / `zh-CN` stored in the settings table (`GET/PUT /api/settings/language`); **single JSON resource store** shared by frontend and backend (`i18n/{locale}.json`, namespaces `ui` + `errors`, ADR 0008 — no .resx, one file per locale).
- react-i18next across all components; first-run wizard language step + settings dropdown; backend coded errors → localized `message` + stable `errorCode` (`LocalizedExceptionFilter`, REST + MCP); existing Knowledge untouched.

### 2.10 Product-grade UI + theme (T12)
- Botanical blue theme (day = mist-blue × amber, night = night-sky blue), top navigation, Instrument Sans / Newsreader / JetBrains Mono, CSS variables + `[data-theme]` (design exploration: `docs/design/`).
- Color theme selector (`light` / `dark` / `system`, persisted in `settings.theme`); anti-FOUC inline script; settings page.

### 2.11 Observability — OpenTelemetry + Aspire dashboard (T13)
- Logs (Serilog.Sinks.OpenTelemetry dual-write) + traces + metrics, **enabled by default**; `service.name=agent-context`; resource attributes from `OTEL_SERVICE_NAME` / `OTEL_RESOURCE_ATTRIBUTES`.
- OTLP endpoint from `OTEL_EXPORTER_OTLP_ENDPOINT`; local Compose/AppHost wiring targets the in-process dashboard, while standalone configuration may use `localhost:4317`; `OTEL_SDK_DISABLED` is the escape hatch; pipeline spans are instrumented (`learning-pipeline.process`).
- The dashboard is hosted by the AppHost in the current image; it is not a separate Compose service.

### 2.12 AppHost mode + dashboard menu + CI/CD (T13/T15 follow-ups)
- **Default (no args)**: the same binary runs as an Aspire DistributedApplication (portal + dashboard resources). Postgres is Aspire-managed locally, while the Docker image receives `ConnectionStrings__Default` and models Postgres as an external resource (`docs/guides/apphost-mode.md`).
- The Docker image exposes the portal on `:8080`; the in-process dashboard is reached at `/monitor/resources` through the portal, while its `:18888` listener remains container-internal.
- UI topbar "Dashboard" entry uses `DASHBOARD_URL` (`GET /api/health/dashboard`); Compose points it at `/monitor/resources`, the canonical Resources surface.
- The Dockerfile carries target-architecture Aspire DCP/Dashboard RID packages into the runtime NuGet cache because `dotnet publish` omits them from `deps.json`; temporary staging stays outside `/app` to avoid duplicate image content.
- **GitHub Actions**: `build.yml` (web build → dotnet build/test on push/PR, one retry for Testcontainers flakiness) and `release.yml` (v* tags → multi-arch linux/amd64+arm64 image to GHCR `ghcr.io/danvic712/agent-context:latest` + tag, then a GitHub Release).

### 2.13 Ops
- Auto-creates the database + applies migrations at startup; Docker Compose = AppHost image + external Postgres(pgvector), with the dashboard in-process; 191/191 tests green (seam + adapter suites).

## 3. Post-MVP Delivery Log

| Ticket | Scope | Delivered |
|---|---|---|
| T9 (#10) | Craft Agents integration + guide skill + full-loop validation | 2026-08-16 |
| T10 (#11) | LLM endpoint configuration | 2026-08-16 |
| T14 (#16) | Platform inference configuration: three tables, multi-provider routes, connection validation, and three-step setup | 2026-08-19 |
| T11 (#12) | Platform localization (en-US/zh-CN, single JSON store) | 2026-08-16 |
| T12 (#13) | Product-grade UI + DB theme + Skill package model | 2026-08-16 |
| T13 (#14) | OpenTelemetry + Aspire dashboard | 2026-08-16 |
| T13 follow-ups | AppHost mode · dashboard menu · CI/CD (GHCR + Actions) | 2026-08-16 |
| T15 (#15) | Complete AppHost image, external PostgreSQL, in-process dashboard, same-origin `/monitor` proxy | 2026-08-18 locally; final Docker/full-loop validation pending |

Earlier milestones were validated end-to-end — see `docs/validation/t11-localization-ui.md`, `docs/validation/t12-ui-skill-package.md`, `docs/validation/t12-redesign-ui.md`, `docs/validation/t13-otel.md`. Current T15 checks are recorded in `docs/validation/t15-docker-build.md`; the T13 Compose topology in that older record is historical.

## 4. Architecture

```
               Users (React UI, botanical theme, light/dark/system)
                            |
        ┌───────────────────┴───────────────────┐
        │   AgentContext (single .NET project)  │
        │   default: portal REST API + UI + MCP │
        │   default: Aspire DistributedApp      │
        │   shared: EF Core / retrieval /       │
        │   learning / BackgroundService        │
        │   in-process dashboard + /monitor     │
        └───────────────────┬─────────────────┘
                            │
                 PostgreSQL (+ pgvector)
                 (no Redis, no Hangfire)
```

- Single project, one entrypoint — no-args startup runs the full 3-in-1 environment (ADR 0006); one DI graph, one DbContext, one config; feature folders + `AppService` convention (CODING_STANDARDS.md).
- Background processing: `BackgroundService` + Postgres-as-queue (ADR 0005); hygiene on `PeriodicTimer`.
- Language and theme preferences live in the `settings` table; inference configuration lives in the three inference tables (ADR 0009, superseding ADR 0003); localization resources live in one JSON store (ADR 0008).

## 5. Tech Stack

- **Backend**: .NET 10, ASP.NET Core, EF Core, Serilog, Microsoft Agent Framework (chat + embeddings), OpenTelemetry (traces/metrics via OTel SDK, logs via Serilog sink), Aspire (AppHost mode).
- **Frontend**: React + TypeScript + shadcn/ui (botanical theme, Tailwind v4), react-i18next (T11), react-markdown + shiki (T12).
- **Data**: PostgreSQL + pgvector; Redis deferred (ADR 0007); Hangfire deferred (ADR 0005).
- **CI/CD**: GitHub Actions (build + test · GHCR multi-arch image + GitHub Release on tags).

## 6. Data Model

```
Workspace ──┬── Domain ──┬── Knowledge (Type/Content/Confidence/Embedding/ConflictGroupId/Status/IsPrivate)
            │            └── Skill (versioned; package files on filesystem)
            ├── Membership ── User
            └── Session ──┬── Agent
                          ├── Usage (source-aware tokens by model; optional route/session bindings)
                          └── ⟶ Knowledge (distilled into)
AppSetting (language, theme) · InferenceConfiguration ── InferenceRoute ── InferenceProvider
```

## 7. Workspace & Visibility

- Personal and Family today; Team planned. Domains live inside workspaces; cross-workspace knowledge flow is explicit only.
- Visibility: domain-level shared/private + per-item private marker. Auth: first-run admin + personal Workspace; invite-link membership — approach flagged pending in the design interview.

## 8. Key Decisions (ADRs)

| # | Decision |
|---|---|
| 0001 | Sessions reported by agents over MCP; no traffic proxy |
| 0002 | Self-hosted Docker Compose first; SaaS later |
| 0003 | Historical single OpenAI-compatible endpoint decision (superseded by 0009) |
| 0004 | MVP scope = learning loop + thin skills + session overview; explicit no-list |
| 0005 | BackgroundService + Postgres-as-queue; Hangfire deferred |
| 0006 | One project, one entrypoint (no-args = full 3-in-1 environment) |
| 0007 | Redis deferred from MVP stack |
| 0008 | Localization resources in one JSON file per locale, shared frontend/backend |
| 0009 | Platform inference via configurable OpenAI-compatible providers and capability routes |

## 9. Testing

- **Primary seam**: application services against Testcontainers pgvector (real DB; LLM faked via `FakeLlmClient`/`FakeSettingsAppService`).
- **Adapter seam**: thin REST (`WebApplicationFactory`, run setup first) + MCP (real stdio process, in-process client) contract tests; plus OTel and web-host smoke tests.
- Baseline 191/191 green, zero functional regressions; OTel stays green by default (T13 acceptance). CI runs the full suite on every push/PR (`build.yml`), retrying once on Testcontainers flakiness.

## 10. Out of Scope (no-list)

Skill marketplace · enterprise SSO/audit · auto memory injection · git-synced skills · per-item ACL · built-in chat UI · traffic proxy · SaaS hosting · Redis · Hangfire · `npx install`-style remote skill install · knowledge translation migration.

## 11. References

- `docs/handoffs/t1-t10-delivered.md` (context bridge for implementers)
- `docs/design/` (UI design exploration — botanical theme lineage)
- `docs/guides/craft-agents-source.md` · `docs/guides/apphost-mode.md` · `docs/skills/craft-agents-guide.md`
- `docs/validation/t9-full-loop.md` · `t11-localization-ui.md` · `t12-ui-skill-package.md` · `t12-redesign-ui.md` · `t13-otel.md`
- `docs/research/competitive-landscape.md` (positioning evidence)
