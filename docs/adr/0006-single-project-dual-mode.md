# One project hosts both the REST API and the MCP server (dual-mode entrypoint)

The platform is a single .NET project with two entrypoints: `--web` runs the ASP.NET Core host (REST API, analytics, UI, MCP over HTTP for compatible clients); `--mcp-stdio` runs the MCP server over stdio for Craft Agents, which connects via local stdio sources. Both share one service registration (`AddApplicationServices`) — one DI graph, one DbContext, one config.

Why: the API and MCP server are the same domain logic seen through two surfaces; splitting them duplicates wiring for no benefit at this scale. Craft Agents' stdio transport is why the MCP surface needs its own process mode rather than in-process `MapMcp()` alone.

Consequence: the same binary serves both roles. If the two surfaces ever diverge, splitting into thin projects over a shared Core is a cheap, well-seamed migration.
