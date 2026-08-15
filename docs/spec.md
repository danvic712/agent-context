# Agent Context — MVP Spec

> Source of truth for decisions: `docs/adr/0001`–`0007` and `CONTEXT.md`. This spec is the buildable collapse of the design interview (2026-08). Terminology follows `CONTEXT.md` exactly.

## Problem Statement

From the setter-upper's perspective: every AI agent I use starts each session from zero. Skills are installed separately per machine, past solutions vanish with each conversation, and I have no visibility into what my agents actually cost or how well they work. When family or a small team share an agent setup, none of that knowledge is shared either — it lives in individual, disconnected sessions.

## Solution

Agent Context is a self-hosted platform (one .NET binary + Postgres) that gives agents a shared context layer: agents report their Sessions over MCP, the Learning Engine distills them into domain-scoped Knowledge with Confidence scores, future Sessions retrieve that Knowledge, Skills are managed centrally, and the UI reports Usage. First integrated agent: Craft Agents, via a stdio MCP source and a guide skill.

## User Stories

1. As a setter-upper, I want to start the platform with `docker compose up` (app + Postgres), so that I can run it self-hosted without cloud dependencies.
2. As a setter-upper, I want a first-run wizard that creates my admin account and personal Workspace, so that the platform is usable in one sitting.
3. As a setter-upper, I want to configure one OpenAI-compatible LLM endpoint (base URL + API key) in settings, so that the Learning Engine can extract and embed.
4. As a user, I want my agent (Craft Agents) to connect to the platform as a local MCP source, so that it can read and write shared context.
5. As an agent, I want to call `save_session` with a domain and a structured summary after a conversation, so that the session is recorded without storing raw conversation.
6. As an agent, I want to pass an explicit domain when reporting a session, so that Knowledge lands in the right Domain.
7. As a user, I want to say "remember" during a conversation, so that the full original context of that session is stored and marked important.
8. As an agent, I want `save_session` to accept a pre-structured Knowledge item, so that a capable agent can skip platform-side extraction.
9. As the Learning Engine, I want to deduplicate incoming sessions against existing Knowledge, so that repeated content corroborates existing items instead of creating duplicates.
10. As the Learning Engine, I want to extract Knowledge items (Problem / Solution / Pattern) from session summaries, so that sessions become reusable Knowledge.
11. As the Learning Engine, I want to detect conflicting Knowledge and mark conflict pairs, so that retrieval shows both sides for judgment.
12. As the Learning Engine, I want to embed Knowledge and store vectors in pgvector, so that semantic search works.
13. As the Learning Engine, I want every Knowledge item to carry a Confidence score (extraction self-assessment, field completeness, cross-session corroboration, novelty), so that retrieval can filter by quality.
14. As a user, I want Confidence to be adjusted by usage feedback, so that Knowledge I confirm gains weight and Knowledge I reject is cleared.
15. As a setter-upper, I want a single switch to disable automatic Knowledge storage, so that I can control when the platform learns.
16. As a user, I want to delete a Knowledge item, so that wrong or obsolete Knowledge is removed.
17. As an agent, I want to call `search_memory` within a domain, so that I can retrieve relevant past Knowledge during a session.
18. As an agent, I want to call `find_similar_solution` with a problem description, so that I can reuse a previously recorded Solution.
19. As an agent, I want retrieval to return Knowledge above a Confidence threshold and to surface conflict pairs side by side, so that I can judge contradictory answers.
20. As the platform, I want stale and low-Confidence Knowledge to decay and be moved to review or archive on a schedule, so that the Knowledge base stays healthy.
21. As a setter-upper, I want to create a Skill (markdown instructions + version + domain) in the UI, so that capabilities are managed centrally.
22. As a setter-upper, I want to edit a Skill and publish a new version, so that improvements are tracked.
23. As an agent, I want to call `get_skill` by slug, so that I can load the right Skill without local installation.
24. As an agent, I want to call `rate_knowledge` (useful / not useful), so that the platform can adjust Confidence from real usage.
25. As a user, I want domain-level visibility (shared vs private) so that a Family Workspace can share a "home" Domain while a personal "dev" Domain stays private.
26. As a user, I want to mark an individual Knowledge item private, so that exceptions to domain-level sharing are possible.
27. As a setter-upper, I want to see a session overview report (sessions, tokens, cost by workspace / domain / agent), so that I understand usage and spend.
28. As a setter-upper, I want the platform to compute cost from token counts against a maintained model pricing table, so that Usage requires no extra collection.
29. As a setter-upper, I want a health view of the Learning Engine (queued sessions, failures, retries), so that I can see the background pipeline working.
30. As a family member, I want to use my own agent app against the shared Family Workspace context, so that I benefit without learning new tooling. *(post-MVP; design seam only)*

## Implementation Decisions

- **One project, dual-mode entrypoint** (ADR 0006): `--web` runs the ASP.NET Core host (REST API, analytics, UI, MCP over HTTP); `--mcp-stdio` runs the MCP server over stdio for Craft Agents. One shared service registration (`AddApplicationServices`) — one DI graph, one DbContext, one configuration.
- **Folders inside the single project**: API controllers / MCP tool definitions / application services / EF Core + retrieval infrastructure / background worker. New MCP tool = a class + one registration; new API endpoint = a controller.
- **Postgres-as-queue** (ADR 0005): `save_session` writes the Session row immediately (`status = pending`). A `BackgroundService` polls for pending Sessions, runs the Learning Engine pipeline, and updates `status` with `next_attempt_at` for retries. Hygiene cleanup runs on a `PeriodicTimer`.
- **Learning Engine as a synchronous service** (`ILearningPipeline.ProcessAsync`): deduplication → LLM extraction → conflict detection → embedding → pgvector. The BackgroundService is only a scheduler; the pipeline is directly invocable so tests and the MCP surface can drive it through the same seam.
- **Confidence** (weights): extraction self-assessment 40% · field completeness 20% · cross-session corroboration 20% · novelty/dedup 20%. Dynamic adjustments: cited-in-retrieval +0.1 (capped); `rate_knowledge` not-useful or user deletion clears; temporal decay reduces long-unused items. Default retrieval threshold 0.5; sub-threshold items are reviewable in the UI.
- **Knowledge shape**: `Problem`, `Solution`, `Pattern`, `Confidence`, source Session reference, Domain, optional private marker, optional conflict-pair links.
- **MCP v1 toolset**: `save_session` (domain, structured summary, optional remember/full-context, optional pre-structured knowledge), `search_memory`, `find_similar_solution`, `get_skill`, `rate_knowledge`. Resources: `skill://{domain}/{slug}`, `knowledge://{id}`. `search_memory` and `find_similar_solution` share one retrieval backend.
- **Platform LLM** (ADR 0003): one configured OpenAI-compatible endpoint (base URL + key) used for extraction and embedding in v1; the config shape leaves room for separate extraction/embedding models later.
- **REST API**: sessions, skills, knowledge (with Confidence + review/delete), analytics overview, settings (LLM endpoint), first-run wizard. Auth per Q23: first-run creates admin + personal Workspace; membership via invite link — **approach flagged pending in the design interview, confirm before building auth**.
- **UI** (React + TypeScript + shadcn/ui): first-run wizard, sessions, skills (create/edit/publish version), knowledge (search, confidence, private marker, delete), analytics overview, settings.
- **Frontend talks to the REST API only**; MCP is the agent-side surface.
- **Data model** (per confirmed ER): Workspace → Domain → Knowledge / Skill; Workspace → Membership → User; Workspace → Session → (Agent, Usage, → Knowledge).
- **Cost**: token counts per model from `save_session`, multiplied by a maintained pricing table.
- **Docker Compose**: two services — the app and Postgres (pgvector image). No Redis (ADR 0007), no Hangfire (ADR 0005).

## Testing Decisions

- **Primary seam — application services** (the `AddApplicationServices` boundary): all behavior is tested by invoking services directly against a real Postgres with pgvector via Testcontainers. This is where behavior lives; both REST and MCP are thin adapters over it.
- **Secondary seam — adapter contract tests**: a small set of end-to-end tests prove the adapters wire correctly — REST via `WebApplicationFactory`, MCP via the SDK's in-process client (call `save_session` / `search_memory` and assert results).
- **Good test** = asserts external behavior through the service boundary against a real database; no mocking of the database; no assertions on implementation details.
- **Learning Engine** is tested synchronously through the seam (invoke `ILearningPipeline.ProcessAsync` on a recorded Session, assert Knowledge rows, Confidence values, conflict pairs, dedup behavior). The BackgroundService scheduling itself is not behavior-tested beyond a thin smoke test.
- **Retrieval** tests assert semantic relevance and Confidence threshold filtering against seeded Knowledge in pgvector.
- **Stack**: xUnit, Testcontainers (pgvector image). No prior art in-repo (greenfield) — these tests establish the pattern.

## Out of Scope

- Skill marketplace, Git-synced Skills, per-item ACL matrix, enterprise SSO/audit, auto memory injection, built-in chat UI, traffic proxy, SaaS hosting, Redis, Hangfire.
- Agents beyond Craft Agents (other MCP-over-HTTP clients are structurally supported by `--web` but not validated in MVP).
- Family-Workspace member flows (invite links, per-member agent apps) — design seam only; pending Q23 auth decision.
- Knowledge curation beyond hygiene (no human review workflow beyond delete/review list).
- Analytics beyond the session overview (no skill-effectiveness or trend charts in MVP).

## Further Notes

- First validation scenario: the setter-upper's own daily Craft Agents usage in a "dev" Domain (this project's knowledge).
- The Craft Agents integration ships as: the `--mcp-stdio` mode + a guide skill in the repo teaching when to report sessions, how to structure summaries, and when to search.
- Positioning (ADR 0004 / research): the learning loop is foundation, not the selling point; the differentiator is governed, cross-agent context with Usage intelligence.
- Anything contradicting these decisions should surface the ADR explicitly rather than silently override.
