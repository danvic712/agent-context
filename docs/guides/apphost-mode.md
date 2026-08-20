# Default startup — the full 3-in-1 environment (UI + MCP + dashboard)

> The public entrypoint is one mode: **running the binary with no arguments
> starts everything** — the Host runs as an Aspire DistributedApplication and
> the dashboard provides the full Resources view (service list, states,
> dependency graph, console logs) in both local and Docker Compose runs. The
> same T13 OTel stack exports all three signals to it. There are no `--apphost`
> / `--web` / `--portal` flags to remember.

## Run

```bash
# from the repo root; requires Docker (for the pgvector container) and .NET 10
dotnet run --project src/AgentContext.Host
```

What happens:

- The Host runs an in-process Aspire dashboard on the fixed internal port
  `18888` by default (override with `DASHBOARD_PORT`). Browser traffic uses the
  portal's same-origin `/monitor/resources` route; the dashboard listener is
  not a separately published Compose port. Add
  `ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS=true` for no login.
- A pgvector Postgres container (`pgvector/pgvector:pg17`) is started with a
  fixed password (`agent_context`) and the named volume `agentcontext-pgdata`.
- The portal runs as a child process of the same binary (internal
  `HOST_MODE=portal` role), bound to
  `http://localhost:8080` with `ConnectionStrings__Default` and
  `Skills__Directory` injected by Aspire. The Data Protection key ring is
  persisted under `/data/agent-context/data-protection-keys` in Docker Compose
  so provider API keys remain decryptable after container recreation.
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

## Relationship to Docker Compose

Both run styles use the same AppHost code path:

| | `docker compose up` | `dotnet run` (default) |
|---|---|---|
| Dashboard | in-process, portal `/monitor/resources` (internal :18888) | in-process, portal `/monitor/resources` (internal :18888) |
| Resources view | **yes** | **yes** |
| Portal | child process, :8080 | child process, :8080 |
| Postgres | external Compose service | Aspire-managed container |

They share the portal port 8080, so run one at a time. In Docker the
Dashboard port 18888 is internal to the container. The Docker image receives
`ConnectionStrings__Default`, so AppHost models Postgres as an external resource
instead of trying to start a nested database container. The internal
`HOST_MODE=portal` marker scopes only the child process; it is not a user-facing
startup mode.

## Implementation notes

- `src/AgentContext.Host/AppHost/AppHostRunner.cs` — the
  `DistributedApplication` model; the portal runs as a child process of the
  same binary, scoped to the portal role via the internal `HOST_MODE` env
  (not a user-facing mode). A supplied `ConnectionStrings__Default` makes
  Postgres an external resource for the container image.
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
- The Dockerfile stages `Aspire.Hosting.Orchestration.<rid>` and
  `Aspire.Dashboard.Sdk.<rid>` into the runtime NuGet cache. The packages are
  required by DCP/dashboard startup but are not emitted into `deps.json` by
  `dotnet publish`; the staging directory remains outside `/app` to avoid
  duplicating roughly 229 MB in the final image.
- `OTEL_SERVICE_NAME` now wins over the `agent-context` default
  (`OtelDefaults.GetServiceName`) so the Serilog OTLP sink and the OTel SDK
  attribute telemetry to the same name; compose (no override) keeps
  `agent-context`.
