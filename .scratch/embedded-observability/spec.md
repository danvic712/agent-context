# Embedded Observability and Trace-first diagnostics

This is the frozen implementation spec for the accepted design in [ADR 0010](../../docs/adr/0010-embedded-observability-local-diagnostics.md). It describes the target behaviour; it does not imply that the current T13 OpenTelemetry implementation already provides local Trace persistence.

## Problem Statement

Agent Context can currently export OpenTelemetry signals to an external OTLP Collector, but it cannot provide a durable, local diagnostic context when no collector is configured or when an operator needs to inspect one operation from inside the product. Operators need to answer “what happened in this operation?” across HTTP, MCP, Learning Engine, background work, outbound HTTP, database activity, and structured logs without making business requests depend on telemetry I/O or persisting secrets and payloads.

## Solution

Add Embedded Observability: a platform-local diagnostic surface that captures in-process Traces, timed Spans, and sanitized Serilog Observability Logs and persists them asynchronously in PostgreSQL. Trace is the primary domain object. A Request is only an HTTP or MCP root-Trace projection, and an Error is a Trace status or Log-level filter rather than a separate product surface.

Local persistence is enabled by default and uses bounded, separate Span and Log queues. Every admitted Span is idempotent; child Spans may arrive before a root summary; and queue pressure can mark a Trace `incomplete` but can never make a partial Trace appear complete. External OTLP export remains parallel and optional. `OTLP_COLLECTOR_EXPORT_DISABLED=true` disables only external Collector export; it does not disable local capture or persistence.

V1 is for a trusted internal network or reverse-proxy boundary and has no application-level authentication or authorization. Strict sanitization is therefore a hard contract: sensitive values are excluded before enqueueing and before persistence.

## User Stories

1. As an operator, I want every in-process HTTP operation to produce a durable Trace, so that I can diagnose a request after it has completed.
2. As an operator, I want MCP calls to appear as Trace roots, so that remote agent interactions are diagnosable through the same surface.
3. As an operator, I want Learning Engine and BackgroundService work to produce Trace roots, so that failures outside an HTTP request are not invisible.
4. As an operator, I want child Spans for outbound HTTP calls, database activity, and application activities, so that a Trace explains its downstream work.
5. As an operator, I want a Trace to include correlated Observability Logs, so that structured application events can be understood in the operation's timeline.
6. As an operator, I want Trace persistence to work without an external OTLP Collector, so that local diagnosis is available in a self-hosted installation with no telemetry backend.
7. As an operator, I want external OTLP export to remain independently configurable, so that I can keep using an existing Collector without coupling it to local persistence.
8. As a deployer, I want `OTLP_COLLECTOR_EXPORT_DISABLED` to disable only Collector export, so that turning off the external destination never unexpectedly removes local Trace data.
9. As an operator, I want local capture to be enabled by default, so that a new installation is diagnosable without extra setup.
10. As an operator, I want to turn local persistence on or off, so that I can control local storage while leaving the external-export decision independent.
11. As an operator, I want to set the minimum persisted Log level, so that routine local storage can be reduced without removing Trace structure.
12. As an operator, I want separate Log and Trace retention periods, so that high-volume Logs and lower-volume Trace structure can be managed independently.
13. As an operator, I want retention to use event time and batched deletion, so that cleanup is predictable and does not create a large blocking transaction.
14. As an operator, I want a Trace list filtered by time, root kind, status, duration, route, and method, so that I can narrow a large diagnostic history quickly.
15. As an operator, I want to find a Trace by its `trace_id`, so that a correlation ID from a log or external system opens the exact diagnostic context.
16. As an operator, I want stable cursor pagination with bounded ranges and page sizes, so that the list remains predictable as data grows.
17. As an operator, I want Trace detail to show a summary, a span waterfall/tree, and related Logs, so that I can move from the root operation to the slow or failing child operation.
18. As an operator, I want the UI to show `incomplete` explicitly, so that queue loss is visible and a partial Trace is not mistaken for a complete one.
19. As an operator, I want Requests represented as a Trace list projection, so that the product has one coherent diagnostic model rather than duplicate Request and Trace records.
20. As an operator, I want Errors represented by Trace status and Log level filters, so that a separate error table does not fragment diagnosis.
21. As an operator, I want sensitive request, response, authentication, cookie, token, query-string, and SQL data excluded, so that diagnostic storage does not become a secret or payload store.
22. As an operator, I want bounded field sizes and constrained attributes, so that an unexpectedly large log or attribute cannot exhaust local storage or queue memory.
23. As an operator, I want business requests to continue when the local queue is full, the database is slow, or the Collector is unavailable, so that observability cannot take down the product.
24. As an operator, I want dropped records and incomplete Traces counted and visible to operations, so that loss caused by pressure is measurable rather than silent.
25. As a developer, I want repeated Span delivery to be idempotent, so that retries and shutdown drains cannot duplicate diagnostic data.
26. As a developer, I want a child Span to be accepted before the root summary, so that asynchronous delivery order does not lose downstream context.
27. As a developer, I want local configuration changes to be stored in PostgreSQL, so that the running application has one durable source for product-controlled settings.
28. As a deployer, I want queue, batch, flush, retry, and similar tuning kept outside the product UI, so that operators see only safe product controls while deployment internals remain adjustable.
29. As a maintainer, I want real PostgreSQL, a compiled Host, an HTTP stub, and real HTTP/MCP/background flows in validation, so that the design is proven at the highest useful seams.
30. As a maintainer, I want a runnable Trace-first prototype before formal UI work, so that layout, states, and navigation are validated before implementation becomes expensive.

## Implementation Decisions

- **Domain model:** Trace is the primary diagnostic context identified by `trace_id`; Span is a timed operation inside it; Observability Log is a sanitized Serilog event associated with an Activity context; Request is an HTTP/MCP projection. The physical model is one `traces` root-summary row per `trace_id`, related `spans`, and related `logs`.
- **Root coverage:** V1 captures all in-process root kinds: HTTP, MCP, Learning Engine, and BackgroundService. It includes outbound HTTP, database, and application child Spans where instrumentation exposes them.
- **Capture path:** Use the OpenTelemetry SDK's custom embedded processor/exporter seam to convert completed Activities into local queue records. Keep the external OTLP exporter parallel; do not add an in-process OTLP receiver.
- **Log path:** Keep Serilog as the application logging API and add an Embedded Observability sink. The sink performs lightweight conversion and enqueueing, preserves the message template and structured properties after sanitization, and associates the event with the current Activity when one exists.
- **Local persistence:** Persist through asynchronous workers and separate bounded Span and Log queues. Business work is never synchronously blocked by local persistence. Queue pressure, persistence failures, and retry exhaustion increment operational counters.
- **Queue loss:** Log pressure may drop older Log entries according to policy. Span pressure rejects a Trace as a whole and marks it `incomplete`; later Spans from that Trace do not create a misleading partial complete record.
- **Delivery order and idempotency:** Every admitted Span is idempotently written with a unique `(trace_id, span_id)` key. Upsert Trace summaries so a child can arrive first and the root can later complete or update the summary.
- **Retry and shutdown:** Use finite retries and a bounded shutdown drain. A failed or exhausted write must not block request threads or wait forever during application shutdown.
- **Sanitization:** Apply a strict allowlist before enqueue and persistence. Persist route template, method, status, duration, root kind, and constrained attributes/database summaries only. Exclude request/response bodies, Authorization/Cookie/Token material, query-string values, SQL text, and SQL parameters by default. Enforce bounded sizes.
- **Runtime configuration:** Store local Embedded Observability settings in the existing database settings store. Defaults are `OBSERVABILITY_ENABLED=true`, `OBSERVABILITY_MIN_PERSISTED_LOG_LEVEL=Information`, `OBSERVABILITY_LOG_RETENTION_DAYS=14`, and `OBSERVABILITY_TRACE_RETENTION_DAYS=7`. UI accepts positive retention integers with a reasonable maximum such as 365 days. Internal Channel/Batch/Flush/Retry tuning is stored/configurable for deployment or migration work but is not editable in the UI.
- **External export:** Preserve `OTEL_EXPORTER_OTLP_ENDPOINT` and `OTEL_EXPORTER_OTLP_PROTOCOL` for the external Collector. `OTLP_COLLECTOR_EXPORT_DISABLED=true` disables only that exporter. External sampling remains separate and is delegated to the Collector; local capture is complete by default when local persistence is enabled.
- **Security boundary:** V1 has no application-level authentication or authorization for the observability API/UI. The deployment boundary is a trusted internal network or reverse proxy. Sanitization is mandatory regardless of that boundary.
- **API:** Expose `/api/observability/traces` as the primary list surface and `/api/observability/traces/{traceId}` for detail. Use UTC timestamps, bounded time ranges and page sizes, stable cursor pagination, and no JSONB full-text search in V1. List filters are time, level, status, method, route, `traceId`, duration, and `rootKind=http|mcp|background`. There is no independent Requests API.
- **Trace detail:** Return root summary, ordered parent/span waterfall or tree, and Related Logs. Display relative start, duration, kind/status, and sanitized attributes. Make `incomplete` explicit.
- **Retention:** Retain by telemetry event time. Delete Spans before Trace summaries in batches; delete Logs independently in batches. Retention work is background maintenance and must not block business requests.
- **UI:** Add a Trace-first list and detail experience with filters, cursor loading, empty/loading/error/incomplete states, and settings for only the four product fields. Do not add an Errors page or separate Requests page. Match the existing localization and theme system.
- **Prototype gate:** Produce a runnable prototype before formal UI implementation. The prototype must cover the list, detail waterfall/tree, related Logs, filters, incomplete state, and the limited settings surface.
- **Highest useful test seams:** Use the existing application-service seam with real PostgreSQL for schema, persistence, idempotency, sanitization, queue/retention behaviour, and query semantics. Use the compiled Host/WebApplicationFactory adapter seam for real HTTP/MCP/background flows, API contracts, UI static assets, and external Collector/HTTP stubs. Inspect persisted rows and response bodies, not private implementation calls.

## Testing Decisions

- Tests assert externally observable behaviour: persisted records, returned API DTOs, emitted/omitted external export, queue-loss counters, retention results, and UI-visible states. They do not assert a particular processor class, queue implementation, or private method sequence.
- The real PostgreSQL Testcontainers seam is mandatory for migrations, indexes, unique Span delivery, child-before-root ordering, retention, and query pagination. In-memory substitutes are not sufficient for these behaviours.
- Host-level tests must exercise compiled Host behaviour for HTTP roots, MCP roots, background roots, Serilog correlation, and the API. The existing REST and MCP adapter-test patterns are the prior art.
- An HTTP stub must prove outbound HttpClient child Spans without relying on a real third-party service. Database child Spans must be checked against a real PostgreSQL operation.
- Sanitization tests must include every excluded data class and bounded-size inputs, and must inspect both local persistence and external-export payloads.
- Queue-pressure tests must prove business work completes, Log loss follows policy, Span loss is whole-Trace and visibly `incomplete`, and counters are incremented.
- Retention tests must prove event-time cutoffs, independent Log retention, Span-before-Trace deletion ordering, batching, and preservation of newer records.
- Configuration tests must prove defaults, DB-backed changes, UI field allowlisting, internal tuning exclusion, and the independent semantics of `OTLP_COLLECTOR_EXPORT_DISABLED`.
- UI validation must use real loaded API data after the prototype gate, covering Trace list/detail navigation, filters, cursor loading, related Logs, incomplete state, settings constraints, localization, theme, empty state, and API failure state.
- The final validation ticket must run against a compiled Host and real PostgreSQL and record any environment limitation instead of presenting a partial smoke test as full validation.

## Delivery Tickets

The implementation is split into seven published GitHub issues:

1. [T39 / #42 — Define Embedded Observability data model and settings](https://github.com/danvic712/agent-context/issues/42)
2. [T40 / #43 — Capture in-process Traces, Spans, and Observability Logs](https://github.com/danvic712/agent-context/issues/43)
3. [T41 / #44 — Persist Embedded Observability asynchronously and retain it safely](https://github.com/danvic712/agent-context/issues/44)
4. [T42 / #45 — Expose the Trace-first query API](https://github.com/danvic712/agent-context/issues/45)
5. [T43 / #46 — Build the runnable Trace-first prototype](https://github.com/danvic712/agent-context/issues/46)
6. [T44 / #47 — Implement the formal Trace-first UI](https://github.com/danvic712/agent-context/issues/47)
7. [T45 / #48 — Validate Embedded Observability end to end](https://github.com/danvic712/agent-context/issues/48)

The dependency graph is `01 → 02 → 03 → 04 → 06 → 07`, with `01 → 05 → 06`. Ticket 05 can proceed in parallel with capture and persistence after the data vocabulary/model is fixed; ticket 06 is the formal-UI gate and ticket 07 is the final integration/validation gate.

## Out of Scope

- Application-level authentication, authorization, per-Trace permissions, or enterprise audit policy.
- An in-process OTLP receiver or replacement of PostgreSQL with an external telemetry backend.
- Request/response payload capture, secrets, cookies, tokens, query-string values, SQL text, or SQL parameters.
- A separate Requests table/API, a separate Errors table/page, or JSONB full-text search.
- External Collector sampling controls beyond the existing Collector configuration.
- Unlimited queues, infinite retries, unbounded shutdown draining, or blocking business work on telemetry persistence.
- User-editable processor Channel/Batch/Flush/Retry tuning in the initial UI.
- Analytics dashboards beyond the Trace-first diagnostic list/detail and limited settings surface.
- Implementing the feature in this documentation change; code delivery is sequenced by the tickets below.

## Further Notes

- The current T13 code still uses the old endpoint-gated external OTLP behaviour and `OTEL_SDK_DISABLED`; implementation tickets must introduce the accepted local/external separation and the finalized `OTLP_COLLECTOR_EXPORT_DISABLED` name without claiming the target exists beforehand.
- Ticket order is intentionally explicit: the data contract gates capture, capture gates durable queue consumption, durable data gates the API, the runnable prototype gates formal UI, and end-to-end validation gates completion.
- GitHub issues #42–#48 are published with the `ready-for-agent` label and carry the dependency edges in their bodies. The local ticket files were temporary publication inputs and are intentionally removed after publication.
