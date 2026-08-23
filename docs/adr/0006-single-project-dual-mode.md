# Single Host entrypoint across layered class libraries

**Status: accepted; revised 2026-08-23.**

`AgentContext.Host` is the single public entrypoint. Running the binary with no
arguments starts one ASP.NET Core process that serves the REST API, React UI,
and Streamable HTTP MCP endpoint at `/mcp`. PostgreSQL is an external
dependency supplied through `ConnectionStrings__Default`, normally by Docker
Compose.

The API and MCP server share the same application services, DbContext, and
configuration. `ModelContextProtocol.AspNetCore` maps the MCP toolset in the
same process, so remote clients connect by URL without a second host or mode.
The frontend is built into the Host's `wwwroot`.

This revision supersedes the former Aspire-based dual-mode orchestration and
same-origin dashboard proxy. Observability remains available through standard
OpenTelemetry OTLP environment variables when an external collector is
configured.
