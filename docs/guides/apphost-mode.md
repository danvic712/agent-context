# AppHost mode — the dashboard with a Resources view

> T13 follow-up: the standalone (compose) Aspire dashboard shows logs/traces/
> metrics but **no Resources / service view** — the resource service is an
> AppHost-only feature. The Host binary now also runs as an Aspire
> DistributedApplication (`--apphost`) so the dashboard gains the full
> Resources view (service list, states, dependency graph, console logs) while
> the same T13 OTel stack keeps exporting all three signals to it.

## Run

```bash
# from the repo root; requires Docker (for the pgvector container) and .NET 10
dotnet run --project src/AgentContext.Host -- --apphost
```

What happens:

- The Host runs an in-process Aspire dashboard (URL printed on startup,
  typically `http://localhost:5179`, with a login token — or add
  `ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true` for no login).
- A pgvector Postgres container (`pgvector/pgvector:pg17`) is started with a
  fixed password (`agent_context`) and the named volume `agentcontext-pgdata`.
- The portal runs as a child process of the same binary (`--web`), bound to
  `http://localhost:8080` with `ConnectionStrings__Default` and
  `Skills__Directory` injected by Aspire.
- `WithOtlpExporter()` injects the OTLP endpoint + `OTEL_SERVICE_NAME` into the
  portal, so logs/traces/metrics land in the AppHost dashboard attributed to
  the **portal** resource (and `service.name` follows `OTEL_SERVICE_NAME`
  — spec-conformant; without one it defaults to `agent-context`).

## Dashboard

- **资源 (Resources)**: postgres + portal rows with status, source, URL and
  per-resource actions (stop, console logs).
- **图 (Graph)**: the dependency graph (portal → postgres, parameters).
- **控制台 (Console)**: per-resource stdout/stderr.
- **结构化/跟踪/指标**: the T13 three-signal telemetry, same as compose.

## Relationship to docker compose

Both modes are first-class:

| | `docker compose up` | `dotnet run ... -- --apphost` |
|---|---|---|
| Dashboard | compose service, :18888 | in-process, :5179 (token) |
| Resources view | **no** (standalone) | **yes** (AppHost) |
| Portal | container, :8080 | child process, :8080 |
| Postgres | compose service | Aspire container (own volume) |

They share ports (8080/18888), so run one at a time.

## Implementation notes

- `src/AgentContext.Host/AppHost/AppHostRunner.cs` — the
  `DistributedApplication` model.
- `AgentContext.Host.csproj` — SDK is
  `Microsoft.NET.Sdk.Web;Aspire.AppHost.Sdk/13.4.6` (the Aspire SDK ships the
  DCP + dashboard binaries via `Aspire.Dashboard.Sdk.<rid>` /
  `Aspire.Hosting.Orchestration.<rid>`). Its defaults are overridden:
  `TreatProjectReferencesAsResources=false` (keeps the Host→Application
  compile reference) and `IsPublishable=true` (keeps the Dockerfile publish).
  A `CopyWwwrootToOutput` target restores wwwroot copying, which the Aspire
  SDK's StaticWebAssets ordering otherwise breaks.
- `WithOtlpExporter()` is required for `AddExecutable` resources — automatic
  OTLP env injection targets `AddProject` only.
- `OTEL_SERVICE_NAME` now wins over the `agent-context` default
  (`OtelDefaults.GetServiceName`) so the Serilog OTLP sink and the OTel SDK
  attribute telemetry to the same name; compose (no override) keeps
  `agent-context`.
