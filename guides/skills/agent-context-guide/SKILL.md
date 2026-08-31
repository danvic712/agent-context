---
name: agent-context-guide
description: Use when working with Agent Context through an MCP client such as Codex or Craft Agents. Search existing Knowledge before reinventing, load relevant Skills, and report meaningful Sessions.
---

# Agent Context Guide

Agent Context is a shared context layer. Agents report Sessions over MCP, the
Learning Engine distills them into domain-scoped Knowledge, and later Sessions
retrieve that Knowledge. Skills are centrally managed, versioned packages.

## When to use the MCP tools

| Tool | Use it for |
|---|---|
| `search_memory` | Search Knowledge in a Domain before starting familiar work |
| `find_similar_solution` | Find a reusable solution for a concrete problem |
| `get_skill` | Load a known Skill package by domain and slug |
| `save_session` | Report meaningful completed work and its reusable outcome |
| `rate_knowledge` | Give feedback on Knowledge that was useful or misleading |

## Working loop

1. Search the relevant Domain before reinventing a solution. Use
   `find_similar_solution` when the problem resembles a previously solved one.
2. Load a relevant Skill with `get_skill` before applying specialized guidance.
3. After meaningful work, report one Session with a concrete task, conclusion,
   key snippets, and the Skill identifiers used.
4. Rate Knowledge that materially helped or misled the work.

Do not create a Session for trivial conversation or a question that produced no
reusable result. Report one Session per unit of work, not one per message.

## Session summary

Use the following shape for `save_session`:

```text
domain: dev
task: Fix the null-reference crash on the sessions page
conclusion: The projection could return a missing value; guarding it before ordering fixes the crash.
keySnippets:
  - Guard the projection before ordering.
  - Regression check: load the sessions page with an empty result.
skillsUsed:
  - code-review
```

Write for a future agent that was not present. Name files, commands, and
configuration values when they are part of the reusable conclusion.

Include the optional Usage payload when the model and token counts are known:
`model`, `inputTokens`, `cachedInputTokens`, and `outputTokens`.

## Privacy and uncertainty

The default Session is a structured summary. Set `remember=true` and provide
`fullContext` only when the user explicitly asks to retain the original
context. Do not put secrets, API keys, or unnecessary private conversation
content in summaries or snippets.

If Knowledge is missing, conflicting, or uncertain, say so and verify it rather
than presenting an unconfirmed result as fact.
