# Handoff — T1–T10 delivered (schema → learning engine → knowledge UI → skills/analytics/hygiene → Craft Agents + LLM config)

> For the next session implementing the remaining MVP tickets.
> This bridges what T1–T10 already shipped. Work from the tickets; this file is
> the context bridge, not a substitute for `docs/spec.md` / `CONTEXT.md` /
> `CODING_STANDARDS.md` / the ADRs.

> **Historical handoff:** this snapshot predates T11–T15. For the current
> single-entrypoint, Streamable HTTP MCP, and in-process dashboard topology, use
> `AGENTS.md`, `docs/spec.md`, and `docs/guides/apphost-mode.md`.

## Status at handoff

- `main` contains T1–T10 — **140/140 tests green** (re-verified 2026-08-16). Build note: two warnings remain in the test project — CS8619 (`tests/.../Testcontainers/McpProcess.cs:29`) and xUnit2013 (`tests/.../SeamTests/ConflictDetectionTests.cs:145`); not blockers, cleanup candidates.
- Closed: #2–#11 (T1–T10). Open: **#12 (T11) — platform localization (en-US/zh-CN, single JSON store)**.

## Repo conventions (must follow — see CODING_STANDARDS.md)

- **Interfaces → `Application/Contracts`** (`AgentContext.Application.Contracts`); **DTOs → `Application/Dtos`**, one type per file; **application-layer enums → `Application/Enums`** (`AgentContext.Application.Enums`, sibling of Dtos); **implementations → feature folders** (`Application/Learning`, `Application/Retrieval`, …) under feature namespaces; services end in `AppService`.
- Any file using the `Domain` entity aliases it: `using DomainEntity = AgentContext.Domain.Entities.Domain;` (CS0118 namespace shadowing). A feature folder must NOT be named `Knowledge` — it shadows the `Knowledge` entity (use `KnowledgeManagement`).
- **AI SDK is Microsoft Agent Framework** (github.com/microsoft/agent-framework): `Microsoft.Agents.AI` + `Microsoft.Agents.AI.OpenAI` packages. Chat = `OpenAI.Chat.ChatClient.AsAIAgent(...)` + `RunAsync<T>` (structured output, sends `json_schema`); embeddings = `IEmbeddingGenerator<string, Embedding<float>>` via `EmbeddingClient.AsIEmbeddingGenerator()`. Do NOT hand-roll chat via `Microsoft.Extensions.AI.OpenAI`.
- **Platform settings live in the DB** (`settings` table via `ISettingsAppService`), not app config. Resolve per call — never cache settings at construction.
- Frontend API calls use **axios** (`web/src/lib/api.ts`, single instance + error interceptor); components in `web/src/components/`, shadcn primitives in `components/ui`. `wwwroot` build output is NOT committed.
- Enums serialize as strings on the REST surface (global `JsonStringEnumConverter` in Program.cs).

## What already exists (don't rebuild)

### T1 — Schema (0a34d3e … `8e54d53`)
- Entities: `Workspace` / `Domain` / `User` / `Membership` / `Session` / `Knowledge` / `Skill` / `Usage` (`AgentContext.Domain/Entities`); enums in `AgentContext.Domain/Enums.cs` (`SessionStatus` Pending/Processing/Completed/Failed; `KnowledgeType` Problem/Solution/Pattern; `KnowledgeStatus` Active/Review/Archived).
- `Session` carries the Postgres-as-queue state: `Status/NextAttemptAtUtc/ErrorCount/LastError/ProcessedAtUtc`. `Knowledge` carries `Type/Title/Content/Confidence/Embedding (vector(1536))/ConflictGroupId/SourceSessionId/Status/IsPrivate` with an HNSW index on `Embedding`.
- Migrations in `AgentContext.Infrastructure` (all entities + pgvector extension). Fluent `IEntityTypeConfiguration` classes in `Configurations/`. Design-time factory via `DbContextOptionsFactory` (used by both runtime and tests).

### T2 — Session recording (caf87ee)
- `ISaveSessionAppService` → `SaveSessionAppService` (`Application/Sessions`): persists Session + Usage (model/tokens), resolves/creates the domain tag, honours `Remembered` (full context only when asked). Sessions land as `Pending`.
- REST `POST /api/sessions` + MCP `save_session`. Seam tests via Testcontainers pgvector; adapter tests via `WebApplicationFactory` and the MCP in-process client (`McpProcess.CreateClientAsync(connectionString)` runs the real `--mcp-stdio` binary).

### T3 — Learning Engine pipeline (9a8620b, b5d717a, 25ba606)
- `ILearningPipelineAppService` (`Application/Learning`, synchronously invocable): atomic claim (Pending / Failed-due → Processing) → LLM extraction → confidence → embedding → dedup/conflict → pgvector → Completed/Failed.
- Confidence (T3): `0.4×selfAssessment + 0.2×fieldCompleteness`, capped 0.6 (`LearningPipelineDefaults`). Corroboration/novelty weights are T4/T5 hooks.
- LLM endpoint stored in the **settings table** (`ISettingsAppService` in `Application/Settings`, keys in `SettingKeys`); `LlmClient` re-reads it per call so changes apply without restart. Pipeline **idles** (never fails Sessions) when the LLM is not configured.
- Retry (ADR 0005): `Status=Failed` + `ErrorCount++` + `LastError` + `NextAttemptAtUtc = now + 30s×2^attempts` (cap 1h, budget 5). Exhausted budget → stays Failed, visible, never deleted. **Successful retries clear LastError/NextAttemptAtUtc** (ErrorCount stays as audit).
- Worker (`Host/Workers/SessionProcessingWorker`) ticks every 5 s and calls `ProcessNextAsync` — same seam as direct invocation (AC5).
- Gotchas: `ChatClientAgent.RunAsync<T>(message, AgentSession?, JsonSerializerOptions?, options?, ct)`; `OpenAIClientOptions.Transport = new HttpClientPipelineTransport(httpClient)` for stub tests; `GenerateAsync` only has the collection overload; `IValidateOptions<T>` via plain `AddSingleton`; capture request bodies inside the stub `HttpMessageHandler` (the SDK disposes content after sending); `AsNoTracking` after `ExecuteUpdateAsync`.

### T4 — Retrieval + conflict pairs (3abe2e7)
- `IRetrievalAppService` → `RetrievalAppService` (`Application/Retrieval`): `search_memory(domain, query, minConfidence=0.5, Top 10)` and `find_similar_solution(domain, problem)` — domain-scoped, cosine-ranked, Confidence ≥ threshold, conflict-group partners appended side by side (Score 0, deliberately bypassing the threshold — judgement needs both sides).
- **Conflict detection at insert** (pipeline): similarity band `[0.6, 0.9)` → shared `ConflictGroupId` (joins an existing group); `≥ 0.9` dedups/corroborates (+0.05 capped); `< 0.6` standalone. Batch-internal conflicts handled in memory (`FindRelatedInBatch`).
- REST: `GET /api/knowledge/search` + `/api/knowledge/similar-solution`. MCP: `search_memory` + `find_similar_solution` (`Host/Mcp/KnowledgeTools.cs`).

### T5 — Knowledge management UI + rate_knowledge (97ce17e)

- `IKnowledgeAppService` (`Application/KnowledgeManagement` — feature folder must NOT be named `Knowledge`, it shadows the entity): knowledge list with Confidence + source session; review list with threshold (`ReviewKnowledgeResult { Threshold, Items }` — the frontend never hardcodes the threshold); private marker; physical delete; rate.
- `rate_knowledge` semantics (ticket definition, not the spec's citation mechanism): useful → Confidence +0.1 (cap 1.0); not-useful → cleared into review. Constants `RateConfidenceBump` / `MaxConfidence` in `LearningPipelineDefaults`.
- REST: `GET /api/knowledge`, `GET /api/knowledge/review`, `PATCH /api/knowledge/{id}` (isPrivate), `DELETE /api/knowledge/{id}`, `POST /api/knowledge/{id}/rate`; KeyNotFound → 404 (same pattern as SessionsController).
- MCP: `rate_knowledge` (`Host/Mcp/KnowledgeTools.cs`, injected `IKnowledgeAppService`).
- UI: `web/src/components/knowledge-manager.tsx` (list / review / private / rate); axios single instance + error interceptor in `web/src/lib/api.ts`; shadcn primitives in `components/ui`. `wwwroot` build output is NOT committed.
- Gotchas: EF can't `OrderBy` after a projection (order on the entity before `Select`, static `Expression<Func<>>` projection); `KnowledgeListItem` must include `CreatedAtUtc` for sorting.
- Single-user MVP: private items always visible to the owner (AC5 seam, not filtered).
- Gotcha: MCP end-to-end tests use a local `HttpListener` `/embeddings` stub (LLM config points at it); `WebApplicationFactory` tests use `ConfigureTestServices` to swap in a fake `ILlmClient`; test vectors for cosine similarity are hand-built unit vectors `(c, √(1−c²))`.

## Testing patterns

- Primary seam: application services against **Testcontainers pgvector** (`PostgresTestBase`, one container per test; `AsNoTracking` after `ExecuteUpdateAsync`). Database is never mocked — the **LLM is faked** (`Fakes/FakeLlmClient`: deterministic content-hash embeddings, canned extractions, configurable failures; `Fakes/FakeSettingsAppService`).
- Secondary seam: thin adapter tests — REST via `WebApplicationFactory` (run the setup wizard first to create the workspace), MCP via the real stdio process.
- Startup behaviour: the web host **auto-creates the database if missing**, then applies migrations (`IRelationalDatabaseCreator.Exists/CreateAsync` + `MigrateAsync`) — covered by `DatabaseAutoCreateTests`.

## Backlog (open tickets)

- **#12 T11** — Platform localization (en-US/zh-CN, single JSON store, DB-configured language); no open blockers.
- **#13 T12** — Product-grade UI refactor (Direction D, ref. `docs/design/ui-direction-d.html`) + color theme selector + Skill package model (filesystem storage); coordinates with #12 on i18n structure.
- **#14 T13** — OpenTelemetry (logs+traces+metrics, enabled by default) + Aspire dashboard service in compose.

## Notes

- Conflict pairs (T4) and hygiene decay / review-archive (T8) are delivered. The **citation +0.1** dynamic Confidence bump (retrieval-cited knowledge gains weight) is still NOT implemented — only the `rate_knowledge` path adjusts dynamic Confidence so far. If T11 (localization) touches Confidence, keep this in mind; otherwise it is a candidate follow-up.
- Knowledge list/review UI + `rate_knowledge` already exist (T5, closed) — do not rebuild.
