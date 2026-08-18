# T13 — OpenTelemetry (logs + traces + metrics) + Aspire dashboard validation

> Issue #14 · 2026-08-16 · full `docker compose up` stack — the dashboard is a
> default service and the portal exports all three signals to it with zero extra
> configuration. What was checked, what the dashboard showed, and the port/protocol
> reality that differs from the ticket's letter.

> **Historical record:** this validation predates T15. It records the former
> standalone `aspire-dashboard` Compose service and its port mappings. The current
> topology is the complete AppHost image with an in-process dashboard behind the
> portal's same-origin `/monitor` proxy; see [`t15-docker-build.md`](t15-docker-build.md).

## Setup

- Stack: `docker compose up -d` with the rebuilt `portal` image (this change)
  — Postgres (pgvector) + portal + **aspire-dashboard** (`mcr.microsoft.com/
  dotnet/aspire-dashboard:latest`, Aspire 13.1.0).
- Dashboard service:
  - UI `http://localhost:18888` (container's own port 18888)
  - OTLP/gRPC host `4317` ← container `18889`, OTLP/HTTP host `4318` ← `18890`
  - `ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true` (local-dev only)
- Portal env: `OTEL_EXPORTER_OTLP_ENDPOINT=http://aspire-dashboard:18889` (the
  **in-network** address — see "port reality" below).
- Standalone (no compose) default in code: `http://localhost:4317` — which is the
  host-mapped port of the same dashboard service, so one default serves both modes.

## Port/protocol reality (deviates from the ticket's letter)

The ticket said the compose default is `http://aspire-dashboard:4317`, but the
dashboard image listens on **18889** for OTLP/gRPC inside the container (host
4317 is only a port mapping — the official standalone docs map `4317:18889`).
So the compose override targets the actual in-network gRPC port. Both signals
use **gRPC** (protocol default `grpc` in `OtelDefaults.GetProtocolName`, honoring
`OTEL_EXPORTER_OTLP_PROTOCOL`), matching the :4317/:18889 convention.

## What was validated (in order)

1. **Compose includes the dashboard (AC)** — `docker compose up -d` started
   `agent-context-aspire-dashboard` alongside postgres + portal; UI reachable at
   :18888 (302 → `/structuredlogs` → 200). Container logs confirm the three
   listeners: frontend `http://[::]:18888`, OTLP/gRPC `18889`, OTLP/HTTP `18890`,
   unsecured mode active.

2. **Logs (AC: Serilog in structured logs, correct severity + resource)** —
   hit `/api/health`, `/`, and `POST /api/sessions` on the portal, then opened
   the dashboard's **Structured logs** page. It listed the portal's Serilog
   output with resource `agent-context`, correct levels (Information/Warning,
   e.g. the `Overriding HTTP_PORTS…` warning from the containerized run), and a
   **trace id on every request log** (`4c38a15`, `90308f1`, …) — log↔trace
   correlation is intact.

3. **Traces (AC: HTTP + Learning Engine pipeline)** —
   - **Traces page**: every HTTP request appears as a span
     (`GET api/health`, `GET /`, `POST api/sessions`) with duration; trace ids
     match the log page ids one-to-one.
   - **Pipeline**: seeded the `settings` table with an LLM endpoint pointing at a
     local OpenAI-compatible stub (chat completions + 1536-dim embeddings), let
     the 5s worker pick up a pending session, and the dashboard showed a
     `agent-context: learning-pipeline.process` trace (0.25s) exactly at the
     pipeline run's timestamp. The stub's extraction produced 2 Knowledge rows,
     session → Completed (the platform's own pipeline worked end-to-end through
     the OTel-instrumented path). Test-side proof that the span carries
     `session.id` / `knowledge.created` / `pipeline.outcome` tags lives in the
     adapter suite (see below).

4. **Metrics (AC: core metrics visible)** — Metrics page, resource
   `agent-context (应用程序)` present. Instrument tree shows the ASP.NET Core
   built-in meters **and** `System.Net.Http` (enabled via `AddMeter`): opening
   `http.server.request.duration` renders the time-series chart with dimension
   filters `http.request.method` (GET/POST), `http.response.status_code`
   (200/201), `http.route` (api/health, api/sessions), `network.protocol.version`
   (1.1), `url.scheme` (http). Kestrel/Routing/MemoryPool meters also present.

5. **Escape hatches (AC: OTEL_SDK_DISABLED / empty endpoint don't break the
   app)** — covered by `OtelObservabilityTests` (real `Program.cs` boot via
   WebApplicationFactory): with the env var or an empty endpoint the app serves
   normally and the trace/metric providers are **not registered**; with a custom
   endpoint they are. The same gating also skips the Serilog OTLP sink.

6. **Suite stays green with OTel on (AC)** — full suite: **188/188 pass**
   (T12 baseline 173 + 15 new: 11 `OtelConfigTests` + 4 `OtelObservabilityTests`),
   run with OTel enabled by default in every WebApplicationFactory boot (the OTLP
   exporter to the unreachable localhost:4317 only fails exports in the
   background; it never affects request handling).

## Notes / gotchas

- **gssapi stderr noise**: `Error: libgssapi_krb5.so.2: cannot open shared
  object file` appears once in the portal container logs — .NET's GSSAPI probe on
  Linux gRPC channels; harmless, export works.
- **`psql -c` runs one transaction**: a mid-script error rolls the whole batch
  back (bit us while cleaning the LLM settings + knowledge rows; run deletes in
  separate `-c` invocations).
- **Aspire metrics UI**: resource must be selected in the dropdown; instruments
  are already expanded in the DOM (`aria-expanded=null` on the group nodes).
- **Serilog.Sinks.OpenTelemetry 4.x API**: `OpenTelemetrySinkOptions.Endpoint` is
  a **string** (not Uri), and the `IncludedData` flags were renamed —
  `TraceIdField | SpanIdField` (there is no `SpanStartTime` member anymore).
- **Dashboard telemetry has no REST API** in current versions — the
  `/api/telemetry/*` endpoints return the SPA HTML; verify via the UI pages.
