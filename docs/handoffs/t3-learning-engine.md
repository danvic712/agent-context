# Handoff — T3: Learning Engine pipeline

> For the next session implementing [issue #4](https://github.com/danvic712/agent-context/issues/4).
> Read first: `docs/spec.md` (§6.3 Learning Engine, Testing Decisions) · `CONTEXT.md` (terminology) ·
> `docs/adr/0003` (LLM endpoint) · `docs/adr/0005` (Postgres-as-queue) · `CODING_STANDARDS.md` (repo conventions).
> Work from the ticket; this file is the context bridge from T1/T2, not a substitute for the spec.

## Ticket (issue #4)

Pipeline as a synchronously-invocable service: `BackgroundService` polls pending Sessions,
then `ILearningPipeline.ProcessAsync` runs **dedup → LLM extraction (configured OpenAI-compatible
endpoint) → Knowledge items (Problem/Solution/Pattern + initial Confidence) → embedding → pgvector**.
Retries via `next_attempt_at`; failures recorded.

**Acceptance criteria**
1. LLM endpoint (base URL + key) configurable via settings
2. A pending Session produces Knowledge rows with Confidence in pgvector
3. Duplicate content corroborates existing Knowledge instead of creating duplicates
4. Extraction failures set retry state (`next_attempt_at`); permanent failures are visible, not lost
5. Pipeline runs identically invoked directly through the seam (tests) and via BackgroundService

Blocked by #3 — **done** (T1+T2 shipped). No blockers left.

## What already exists (don't rebuild)

- **Schema (T1)**: `Session` has `Status/NextAttemptAtUtc/ErrorCount/LastError/ProcessedAtUtc`;
  `Knowledge` has `Type/Title/Content/Confidence/Embedding (vector(1536))/ConflictGroupId/SourceSessionId/
  Status/IsPrivate`, with HNSW index on Embedding. Migrations in `AgentContext.Infrastructure`.
- **Session recording (T2)**: `SaveSessionAppService` + REST `POST /api/sessions` + MCP `save_session`.
  Sessions land as `Pending`.
- **Worker shell (T2)**: `SessionProcessingWorker` (Host/Workers) ticks every 5 s, opens a scope,
  calls `ISessionProcessingAppService.MarkProcessedAsync` (atomic `ExecuteUpdateAsync`:
  Pending → Completed). **T3 replaces this core with the pipeline**: a tick should claim a pending
  session and run `ILearningPipeline.ProcessAsync` instead of blindly completing it.
- **DI**: everything registers in `Application/ServiceCollectionExtensions.AddApplicationServices`
  (shared by `--web` and `--mcp-stdio`). Worker is registered only in `Host/Program.cs` (web mode).

## Hard constraints (spec / ADRs)

- **ADR 0003**: one OpenAI-compatible endpoint (base URL + key) serves **both extraction and
  embedding** in v1; the config shape must leave room for separate extraction/embedding models later.
  `LlmOptions { BaseUrl, ApiKey, Model, EmbeddingModel? }` via `IOptions` from configuration
  (env-overridable; the compose app env is the settings surface — DB-backed settings UI is a later
  ticket, don't build it here unless the ticket says so).
- **Confidence (spec §6.3)**: extraction self-assessment **40%** · field completeness **20%** ·
  cross-session corroboration **20%** · novelty/dedup **20%**. T3 ticket says "initial Confidence
  from self-assessment + field completeness" — implement the first two weights now (0.6 max),
  leave corroboration/novelty hooks for T4/T5 (retrieval feedback). Dynamic adjustments
  (citation +0.1 capped, rate_knowledge clears, decay) are NOT T3.
- **Postgres-as-queue (ADR 0005)**: no in-memory queue; state lives in `sessions.Status` +
  `NextAttemptAtUtc`. Retry = `Status=Failed` + `NextAttemptAtUtc=now+backoff` + `ErrorCount++` +
  `LastError`. "Permanent failures visible, not lost" = sessions stay `Failed` (with error) after
  the retry budget is exhausted — they are queryable, never deleted.
- **Embedding dimension**: `vector(1536)` is baked into the migration. If the configured embeddings
  endpoint serves a different dimension, the migration must change (new migration) — surface this to
  the user before picking the default model.

## Seams & testing (spec §Testing Decisions)

- **Primary seam — application services against Testcontainers pgvector**: `ILearningPipeline` is
  synchronously invocable (AC5). Tests call `ProcessAsync` on a recorded Session and assert
  Knowledge rows, Confidence values, dedup behaviour, retry state — exactly how T1/T2 seam tests
  work (`PostgresTestBase`, one container per test; remember `AsNoTracking` after
  `ExecuteUpdateAsync`).
- **LLM is external — fake it at the seam**: define `ILlmClient` (extract + embed) in
  `Application/Contracts` (see conventions below); production implementation talks to the
  OpenAI-compatible endpoint (e.g. `Microsoft.Extensions.AI`/OpenAI-compatible REST); tests inject a
  **fake** with canned extraction JSON + fixed embedding vector. The spec forbids mocking the
  *database*, not the LLM. Keep the fake deterministic (fixed 1536-dim vector).
- **Worker scheduling**: thin smoke only (spec). Reuse the T2 pattern
  (`SessionProcessingWorkerSmokeTests`: real DI + short `pollingInterval` + poll the DB).

## Repo conventions (must follow)

- **Contracts/Dtos split** (`CODING_STANDARDS.md`): interfaces → `Application/Contracts`
  (`AgentContext.Application.Contracts`); DTOs → `Application/Dtos`
  (`AgentContext.Application.Dtos`); implementations → feature folders
  (`Application/Learning/…` for T3). Services end in `AppService`.
- **`Domain` entity vs namespace**: any file using the `Domain` entity must alias it
  `using DomainEntity = AgentContext.Domain.Entities.Domain;` (CS0118 namespace shadowing).
- Tests: one container per test via `PostgresTestBase`; `McpProcess.AppBinaryPath` for process tests.

## Suggested TDD slices (red → green, in order)

1. **Settings**: `LlmOptions` + registration + validation (AC1).
2. **`ILlmClient`** (contracts) + OpenAI-compatible implementation (request shape; test request
   construction with a fake `HttpMessageHandler`).
3. **`ILearningPipeline.ProcessAsync` happy path** (fake LLM): pending Session → Knowledge rows
   (Type/Title/Content/Confidence/Embedding persisted in pgvector), session → Completed (AC2).
4. **Dedup**: same-domain duplicate content corroborates the existing Knowledge (bump its
   corroboration/confidence), does not create a new row (AC3). Decide + note the matching strategy
   (vector distance vs LLM judgement) — see open questions.
5. **Retry/failure**: extraction throws → `Status=Failed`, `NextAttemptAtUtc` set, `ErrorCount`
   incremented, `LastError` recorded; after the retry budget, the session stays visible `Failed`
   (AC4).
6. **Worker rewire**: tick claims a pending session and runs the pipeline (smoke test, AC5).
7. Full suite + `code-review` (two axes) + commit on `main`; comment + close #4.

## Open questions to settle with the setter-upper before/during step 3

- Extraction output schema (JSON): propose `{ type, title, content, selfAssessment (0..1) }` (+
  optional `snippets`); confirm.
- Embedding model + dimension (1536 assumed; confirm against the chosen endpoint, e.g.
  `text-embedding-3-small` / Ollama `nomic-embed-text`).
- Dedup matching: embedding cosine threshold vs LLM "is this the same as X?" judgement; both are
  cheap at MVP scale — pick one, keep the other behind the `ILlmClient`/similarity abstraction.
- Retry policy: exponential backoff base (e.g. 30 s, ×2, cap 1 h) + max attempts (e.g. 5); confirm.
- `LLM endpoint settings` source of truth: `appsettings.json`/env via `IOptions` (recommended for
  T3) vs a DB-backed settings table (spec lists a settings REST endpoint — later ticket).

## Status at handoff

- `main` == `origin/main` (T1, T2, Contracts/Dtos conventions shipped; 25/25 tests green).
- Issue #2 and #3 closed by the setter-upper; #4 is `ready-for-agent`, unblocked.
