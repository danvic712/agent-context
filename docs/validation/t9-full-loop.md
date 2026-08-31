# T9 — Full-loop validation write-up

> Issue #10 · 2026-08-16 · setter-upper's real daily usage in the `dev` Domain.
> This is the end-to-end check the Craft Agents integration was shipped for
> (spec "Further Notes" / AC2): report a real session → Learning Engine turns it
> into Knowledge → a later session retrieves it. What worked, what didn't, and
> what it took to get the loop closed.

## Setup

- Platform: local run of the dual-mode binary (`--web` on :8080) + Postgres
  (pgvector, Testcontainers-style local volume) — same DB the MCP server reads.
- LLM endpoint (ADR 0003, stored in the `settings` table): real Azure OpenAI
  endpoint `https://danvic712.openai.azure.com/openai/v1`, Bearer auth,
  extraction model `gpt-5.6-sol`, embedding model `text-embedding-3-large`.
- MCP source: the platform's `--mcp-stdio` registered as a local stdio source
  in the Craft Agents workspace (`sources/agent-context/`), with `config.json` and
  `permissions.json` (see the [current Craft Agents guide](../../guides/integrations/craft-agents.md); this validation record retains the historical stdio setup).

## What was validated (in order)

1. **Tool discovery (AC1)** — `source_test` connected to the stdio server and
   listed the tools: `get_skill, search_memory, save_session,
   find_similar_solution, rate_knowledge` — exactly the v1 toolset (5/5).
   The T1 skeleton `echo` tool was removed as part of T9 so the surface matches
   the published contract.

2. **Report (AC2 →)** — this T9 work was reported as a real session:
   `save_session(domain="dev", task=…, conclusion=…, keySnippets=[…], model,
   agentName)`. The platform persisted the Session as `pending`.

3. **Learning Engine (AC2 →)** — the pipeline claimed the Session, ran LLM
   extraction (gpt-5.6-sol, structured `json_schema` output), embedded the items
   (text-embedding-3-large), and stored 5 Knowledge rows (1 Problem, 2 Solutions,
   2 Patterns), Confidence ≈ 0.58–0.60, all `Active` with pgvector embeddings.
   Session status → `Completed`.

4. **Retrieval (AC2 →)** — a *later* interaction called
   `search_memory(domain="dev", query="register the platform as a local MCP
   source")` and got the reported items back, top hit "Register a stdio MCP
   server as a local agent source" (cosine 0.66). `find_similar_solution`
   with a rephrased problem also returned the recorded Solution (0.65).

5. **Feedback (AC2 →)** — `rate_knowledge(useful=true)` on the top hit raised its
   Confidence 0.584 → 0.684, persisted (the dynamic Confidence loop from T5).

6. **Skill path (AC3)** — the historical `get_skill(domain="dev",
   slug="craft-agents-guide")` call returned v1 of the guide Skill from the
   platform registry (REST and MCP both verified). The current shared package is
   [`guides/skills/agent-context-guide/SKILL.md`](../../guides/skills/agent-context-guide/SKILL.md).

## What worked

- **The full loop is real.** Session → extraction → embedding → retrieval →
  feedback all ran against a live LLM and the real database; nothing was stubbed.
- **Structured extraction quality was high.** gpt-5.6-sol produced well-scoped
  Problem/Solution/Pattern items from a prose summary — the items were immediately
  retrievable and semantically on-target.
- **`--mcp-stdio` as a local stdio source is the right integration shape.** One
  binary, one DB, no ports to expose for the agent side; `source_test` picked it
  up with zero auth ceremony.
- **The guide skill as a platform Skill worked end-to-end** — in-repo markdown,
  registered v1 on the platform, loadable by slug from the agent.

## What didn't / gotchas

- **Azure embedding dimension mismatch (fixed in T9).** `text-embedding-3-large`
  defaults to 3072 dims while the schema is `vector(1536)`; the pipeline correctly
  failed the session (retry-visible, not silent). Fix: pass
  `defaultModelDimensions = 1536` into MAF's `AsIEmbeddingGenerator(...)` in
  `LlmClient`. After the fix, an already-Failed session retried automatically and
  Completed. Lesson: dimension matching is a contract between the embedding model
  and the fixed pgvector column — make it explicit in the client, not a
  deployment surprise.
- **Azure `gpt-5.6-sol` doesn't accept `max_tokens`** (it wants
  `max_completion_tokens`). The MAF layer sends its own options, so this did not
  block extraction — noted for future manual calls against that model.
- **Session-layer "Not connected" after killing the stdio process.** Killing the
  MCP subprocess out from under the running Craft Agents session left the session's
  tool bindings stale until a fresh turn. The MCP server itself was healthy the
  whole time (direct tools/call worked). Operationally: don't `pkill` the stdio
  process mid-session; restart the source through the app instead.
- **`dotnet run` picks up `launchSettings.json`** (dev port 5179) and the built
  binary needs `ConnectionStrings__Default` in env — easy to trip on when
  switching between compose, `dotnet run`, and the raw binary.

## Numbers

| Item | Value |
|---|---|
| Sessions reported | 1 (this T9 work) |
| Knowledge generated | 5 items (1 Problem / 2 Solution / 2 Pattern) |
| Confidence at creation | 0.580 – 0.596 |
| Confidence after `rate_knowledge(useful)` | 0.684 (top hit) |
| Retrieval top-hit cosine | 0.66 (search_memory), 0.65 (find_similar_solution) |
| Tools visible from Craft Agents | 5/5 |

## Verdict

The MVP's first validation scenario is green: an agent can report its daily work
over MCP, the platform turns it into retrievable Knowledge, and a later session
actually gets it back — including the confidence feedback loop. The remaining
rough edges are operational (process management, dimension config) and are now
documented in the integration guide and this write-up.
