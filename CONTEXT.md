# Agent Context

A shared context layer for AI agents — manage skills, memory, sessions, and knowledge across personal, family, and team workspaces. The platform itself is domain-agnostic: it does not specialise in any single area of life or work.

## Language

**Workspace**:
A container for users and their shared knowledge. Personal and Family workspaces come first; Team is a planned extension.
_Avoid_: organization, project

**Domain**:
A classification of knowledge by area of life or work (e.g. dev, home, business). Domains live **inside** a Workspace; knowledge does not flow across workspaces automatically — cross-workspace sharing is always an explicit user action.
_Avoid_: category, topic

**Agent**:
An AI assistant instance connected to the platform that reads and writes shared context through the MCP gateway (e.g. Claude Code, Cursor).
_Avoid_: bot, model

**Session**:
A record of one agent interaction, reported by the agent itself over MCP as a structured summary (task, conclusion, key snippets), including the Skills used, model, and token usage. Full original context is stored only when the user explicitly asks to remember.
_Avoid_: conversation, thread

**Remember**:
The user's explicit command that upgrades a session (or snippet) to full-context storage and marks it important, as opposed to the default automatic summary.
_Avoid_: save, bookmark

**Skill**:
A reusable capability unit an agent can load, managed centrally instead of installed per machine.
_Avoid_: plugin

**Knowledge**:
A reusable answer, pattern, or learning distilled automatically from session summaries, stored with a confidence score and retrievable within its domain. Cross-workspace sharing is explicit only.
_Avoid_: memory, learning record

**Confidence**:
The platform's score for how likely a knowledge item is correct and useful — computed from extraction self-assessment, field completeness, cross-session corroboration, and usage feedback. Used to filter retrieval and flag items for review.
_Avoid_: score, weight

**Usage**:
A source-aware token ledger broken down by model. Reported Session rows attach to a
Session; Learning Engine rows may be sessionless and may retain an optional inference
route and capability binding. Cached input tokens are a subset of input tokens. The
Analytics surface is deferred for redesign; Usage does not contain cost data.
_Avoid_: billing record, telemetry

**Trace**:
A complete diagnostic context for one in-process operation, identified by a `trace_id` and containing one or more timed Spans and related Observability Logs. HTTP requests, MCP calls, Learning Engine runs, and background work can each produce a Trace.
_Avoid_: request, when referring to the complete diagnostic context

**Span**:
A timed operation within a Trace, such as an HTTP server call, outbound HTTP call, database command, or application activity.
_Avoid_: trace, log

**Request**:
An HTTP or MCP view of a Trace rooted at a server operation; it is a query projection for the UI, not a separate diagnostic record.
_Avoid_: separate request record

**Observability Log**:
A persisted Serilog event associated with a Trace or Span when an Activity context exists, retaining structured fields only after the platform's sensitivity and size rules are applied.
_Avoid_: application log, raw log

**Embedded Observability**:
The platform-local diagnostic surface for capturing and persisting Traces, Spans, and Observability Logs in PostgreSQL, independently of optional external OTLP export.
_Avoid_: dashboard replacement, telemetry backend

**Learning Engine**:
The background pipeline that turns session summaries into knowledge — deduplication, chat extraction, conflict detection, embedding, and vector storage — running on the platform's configured inference routes.
_Avoid_: extraction pipeline, knowledge worker
