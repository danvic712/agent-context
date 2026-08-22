# Craft Agents Guide Skill

> **Skill metadata (registered on the platform as a versioned Skill, T9 / issue #10):**
>
> - Domain: `dev`
> - Slug: `craft-agents-guide`
> - Version: 1
> - Name: Craft Agents Guide
> - Description: When and how to report Sessions, structure summaries, and search Knowledge on the Agent Context platform — for agents connected via the stdio MCP source.
>
> This file is the in-repo source of truth (AC3: "documented and versioned in-repo").
> When the skill is re-published on the platform, `Version` bumps and the instructions
> below travel as the skill's markdown body.

## What this is

Agent Context is a shared context layer: agents report their Sessions over MCP, the
Learning Engine distills them into domain-scoped Knowledge with Confidence scores, and
future Sessions retrieve that Knowledge. This guide teaches **when to report, how to
structure a summary, and when to search** — the three judgment calls that keep the
platform useful without turning every conversation into ceremony.

Vocabulary follows the project's `CONTEXT.md` exactly: **Workspace**, **Domain**,
**Agent**, **Session**, **Remember**, **Skill**, **Knowledge**, **Confidence**, **Usage**.

## The five tools (v1 toolset)

| Tool | Purpose |
|---|---|
| `save_session` | Report a Session: domain + structured summary + optional Usage |
| `search_memory` | Retrieve Knowledge within a Domain, ranked semantically |
| `find_similar_solution` | Reuse a recorded Solution for a problem description |
| `get_skill` | Load a centrally-managed Skill by (domain, slug) |
| `rate_knowledge` | Give feedback on a Knowledge item (useful / not useful) |

## When to report a Session

Report when the conversation **produced something worth keeping**: a solved problem, a
decision, a reusable pattern, or a conclusion another session would benefit from.

- **Report after meaningful work** — a task completed, a bug fixed, a design decision
  made, a tutorial walked through. A short debugging aside that ended in "it was the
  config" is exactly the kind of thing the platform should remember.
- **Do NOT report** every trivial exchange (small talk, "what's the weather", a
  yes/no that resolves nothing). Noise dilutes retrieval and inflates Usage.
- **One Session per unit of work**, not one per message. Batch the summary at the end.
- **Always pass a Domain** (`dev`, `home`, `business`, …) so Knowledge lands in the
  right bucket. When in doubt, `dev` is the default for engineering work.
- **Use `remember=true` + fullContext** only when the user explicitly asks to keep the
  full original context ("remember this"). The default is a summary — that's the
  privacy-preserving contract of the platform.
- **Always report the optional Usage payload** when you know it — include the model
  snapshot plus input, cached input, and output token counts. These power the Usage
  view. Analytics reports token counts only; no pricing table or cost estimate is
  maintained.

## How to structure a summary

The quality of the summary is the quality of the Knowledge. `save_session` takes
`task`, `conclusion`, and `keySnippets`:

- **`task`** — what the conversation set out to do, in one sentence. Concrete and
  verifiable: "Fix the null-ref crash on the sessions page", not "worked on stuff".
- **`conclusion`** — the outcome in 1–3 sentences. What was decided, what the fix was,
  what the takeaway is. This is the seed of future Knowledge, so state it as a
  reusable truth: "The crash was caused by a missing null check in `LoadAsync`; guard
  the projection before ordering."
- **`keySnippets`** — 2–5 short, self-contained facts worth keeping verbatim: the exact
  config that worked, the command that unblocked you, the API shape. Not the whole
  transcript — just the fragments a future session would quote.
- **Write for a stranger** — a future agent that wasn't in the conversation must be
  able to act on the summary alone. Expand acronyms the first time, name the files
  touched, and prefer concrete values over prose.

## When to search

Search before you reinvent, and search when you hit a wall:

- **At the start of a session** in a domain you've worked in before: `search_memory`
  for the current task ("how did I set up pgvector here") to pick up relevant past
  Knowledge. Cheap, and it surfaces conflict pairs so you can judge both sides.
- **When a problem looks familiar**: `find_similar_solution` with the problem
  description — the platform returns the best-matching recorded Solution (with any
  conflicting Solutions side by side), or `null` when nothing clears the Confidence
  threshold.
- **When you're about to repeat work**: if a search returns a strong hit, trust it and
  cite it; if it returns nothing useful, proceed and `save_session` afterwards so the
  next session knows what you learned.
- **Confidence is a guide, not a gate**: items above the threshold (default 0.5) come
  back; items below it are reviewable in the UI. Conflict pairs are always returned
  together so you can make the judgment call.

## Feedback loop: rate_knowledge

When you use a Knowledge item and it was genuinely useful, call `rate_knowledge`
(`useful=true`) — the platform bumps its Confidence and it will rank higher for the
next session. If it was wrong or misleading, rate `useful=false` to clear it into the
review list. One small call keeps the shared context honest.

## Loading Skills

Before a task that has a known Skill, call `get_skill` with the domain and slug (e.g.
`get_skill(domain="dev", slug="craft-agents-guide")`) instead of relying on local
installations. Skills are centrally managed and versioned — the latest published
version is always what you get.

## Golden rules

1. **Report** real work, structured as task/conclusion/snippets, with a domain.
2. **Search** before reinventing, and rate what you used.
3. **Respect the privacy contract**: full context only on explicit "remember".
4. **Never fabricate**: if the summary or search result is uncertain, say so rather
   than inventing a confident-looking answer.
