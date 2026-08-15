# Dual-mode entrypoint across layered class libraries (one host project, one DI graph)

The platform is one **host project** (`AgentContext.Host`) with two entrypoints: `--web` runs the ASP.NET Core host (REST API, analytics, UI, MCP over HTTP for compatible clients); `--mcp-stdio` runs the MCP server over stdio for Craft Agents, which connects via local stdio sources. Both share one service registration (`AddApplicationServices`) — one DI graph, one DbContext, one config.

Why: the API and MCP server are the same domain logic seen through two surfaces; splitting them into two hosts would duplicate wiring for no benefit at this scale. Craft Agents' stdio transport is why the MCP surface needs its own process mode rather than in-process `MapMcp()` alone.

> **Revision (2026-08, setter-upper decision):** the codebase is split by system function into layered class libraries — `AgentContext.Domain` (entities/enums), `AgentContext.Infrastructure` (EF Core + migrations), `AgentContext.Application` (application services + `AddApplicationServices`), and `AgentContext.Host` (the dual-mode entrypoint: Program, REST controllers, stdio MCP host). The **dual-mode entrypoint and the single shared DI graph are preserved**; only the source layout is split, so the wiring argument above is unchanged. The frontend lives outside the backend tree at repo-root `web/`, built into the host's `wwwroot`.

Consequence: the same binary serves both roles. If the two surfaces ever diverge, splitting into thin hosts over the shared Application/Infrastructure projects is a cheap, well-seamed migration.
