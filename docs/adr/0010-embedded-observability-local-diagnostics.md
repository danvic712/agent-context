# Embedded Observability is the local Trace system

**Status: accepted (2026-08-24).**

Agent Context treats a Trace as the primary diagnostic record. Embedded Observability captures in-process HTTP, MCP, Learning Engine, and background-work Traces, their timed Spans, and sanitized Serilog Observability Logs, then persists them asynchronously in PostgreSQL. The local pipeline is independent of external OTLP export: `OTLP_COLLECTOR_EXPORT_DISABLED=true` disables only export to an external OTLP Collector and never disables local Trace capture.

The physical model is a `traces` root-summary table with related `spans` and `logs` records. Span writes are idempotent by `(trace_id, span_id)`; child Spans may arrive before the root summary; and a bounded queue may mark a Trace `incomplete`, but must never expose a silently partial Trace as complete. Local persistence is enabled by default and protects request threads with separate bounded queues, finite retries, and bounded shutdown draining.

Embedded Observability uses a strict sensitivity whitelist. Request or response bodies, authorization and cookie material, token-like values, query-string values, SQL text, and SQL parameters are not persisted by default. V1 has no application-level authentication or authorization for this surface; it is intended for a trusted internal network or reverse-proxy boundary, so sanitization remains mandatory even inside that boundary.

The UI is Trace-first. A Request is only an HTTP or MCP projection of a root Trace, and there is no separate Requests data model or error page. The four product settings exposed in the UI are enabled, minimum persisted log level, log retention days, and Trace retention days. Other local runtime tuning remains deployment/internal configuration, while the external collector endpoint/protocol and `OTLP_COLLECTOR_EXPORT_DISABLED` remain external-export controls.

Why: a local Trace view provides a durable diagnostic context without making PostgreSQL dependent on an external collector, while the external exporter remains useful for operators who already have an observability backend.

Consequences:

- The implementation adds local Trace, Span, and Observability Log persistence rather than turning the application into an in-process OTLP receiver.
- Capture and persistence must be decoupled so database latency, collector failures, and queue pressure cannot block business requests.
- A Trace detail view and filtered Trace list are the primary API/UI contracts; Requests and Errors are projections or filters rather than additional product surfaces.
- Retention is based on telemetry event time, deletes Spans before Trace summaries, and deletes Logs independently in batches.
- The current T13 external-OTLP implementation remains the runtime baseline until the tickets in the embedded-observability spec are implemented; this ADR records the accepted target architecture.
