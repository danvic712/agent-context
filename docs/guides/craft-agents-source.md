# Registering Agent Context as a Craft Agents local MCP source

> **Superseded (T14):** the platform no longer ships a stdio MCP mode — the
> single entrypoint runs everything and exposes the toolset over Streamable
> HTTP at `/mcp` (see [`craft-agents-docker-source.md`](./craft-agents-docker-source.md)).
> This page is kept as a historical record of the T9 (issue #10) integration,
> when the platform shipped the `--mcp-stdio` mode (ADR 0006).

## Prerequisites

- The platform is running (or reachable): Postgres on `localhost:5432`, and the host
  binary available locally (`dotnet run -- --mcp-stdio` or the built binary).
- The shared database has been set up (first-run wizard creates the admin account +
  personal Workspace). The MCP server talks to the same database as `--web`.

## 1. Source directory

A Craft Agents source is a folder under the workspace's sources directory, e.g.:

```text
~/.craft-agent/workspaces/<workspace>/sources/agent-context/
├── config.json
├── guide.md
└── permissions.json
```

## 2. config.json — stdio transport

The MCP server runs over stdio (`--mcp-stdio`), so the source is a local stdio
transport whose command is the platform's built binary:

```json
{
  "id": "agent-context_<random-8-hex>",
  "name": "Agent Context",
  "slug": "agent-context",
  "enabled": true,
  "provider": "agent-context",
  "type": "mcp",
  "icon": "🧠",
  "tagline": "Shared context layer — report sessions, search knowledge, load skills",
  "mcp": {
    "transport": "stdio",
    "command": "dotnet",
    "args": [
      "/absolute/path/to/AgentContext.Host.dll",
      "--mcp-stdio"
    ],
    "env": {
      "ConnectionStrings__Default": "Host=localhost;Port=5432;Database=agent_context;Username=agent_context;Password=agent_context"
    },
    "authType": "none"
  }
}
```

Notes:

- `command`/`args` must point at the **same binary** as the running platform (the
  single dual-mode binary, ADR 0006) — a dev build or a published copy both work.
- `ConnectionStrings__Default` must resolve to the same Postgres the web app uses.
- `authType` is `none`: the MCP surface is unauthenticated in MVP (single-tenant,
  local source). Auth is a later concern (spec: "approach flagged pending").

## 3. permissions.json — Explore-mode access

The five tools split into read (search/get) and write (report/rate) surfaces. Allow
the read tools in Explore mode; `save_session` and `rate_knowledge` are the agent's
core reporting duty and are available in normal (Execute) mode:

```json
{
  "allowedMcpPatterns": [
    { "pattern": "search", "comment": "search_memory / find_similar_solution — read-only retrieval" },
    { "pattern": "get", "comment": "get_skill — read-only skill loading" },
    { "pattern": "rate", "comment": "rate_knowledge — feedback drives Confidence (read-oriented)" }
  ]
}
```

## 4. guide.md — usage documentation for the agent

The source's `guide.md` teaches the connected agent when to use the platform. The
canonical text ships in-repo at `docs/skills/craft-agents-guide.md` (registered on the
platform as the `dev/craft-agents-guide` Skill, version 1); the source's `guide.md`
should summarize it in the agent's own context:

```markdown
# Agent Context

Shared context layer for my agent workspace. Report sessions after real work, search
before reinventing, and load centrally-managed skills.

## Scope
- Domains: `dev` (default for engineering work)
- Five tools: save_session, search_memory, find_similar_solution, get_skill, rate_knowledge

## Guidelines
- Report a Session after meaningful work: task / conclusion / keySnippets + domain
- Pass model + token counts so Usage analytics work
- Search the domain at session start; rate knowledge you actually used
- Full-context "remember" only when the user explicitly asks

## Examples
- After fixing a bug: save_session(domain="dev", task="...", conclusion="...", keySnippets=[...])
- Before starting a familiar task: search_memory(domain="dev", query="how did I set up ...")
- Reusing a solution: find_similar_solution(domain="dev", problem="...")
- Loading guidance: get_skill(domain="dev", slug="craft-agents-guide")
```

## 5. Validate

Run the source test / activation flow (Craft Agents `source_test`) against the new
source. A successful connection reports **all five v1 tools**:

`save_session`, `search_memory`, `find_similar_solution`, `get_skill`, `rate_knowledge`

## Full-loop validation

See [`docs/validation/t9-full-loop.md`](../validation/t9-full-loop.md) for the
end-to-end check that was actually run against this setup (report → Knowledge →
retrieve), including what worked and what didn't.
