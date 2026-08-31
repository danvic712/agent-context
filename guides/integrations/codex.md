# Connect Codex

Agent Context exposes one Streamable HTTP MCP endpoint. Codex can use that
endpoint from the desktop app, CLI, or IDE integration.

## Prerequisites

Start the local deployment and finish the first-run wizard:

```bash
docker compose up -d
```

Then open `http://localhost:8080` and configure the Chat and Embedding routes.
The local MCP endpoint is:

```text
http://localhost:8080/mcp
```

## Add the MCP server

With the Codex CLI, register the server by URL:

```bash
codex mcp add agent-context --url http://localhost:8080/mcp
```

Alternatively, add the equivalent entry to Codex configuration:

```toml
[mcp_servers.agent_context]
url = "http://localhost:8080/mcp"
```

Codex stores user-level configuration in `~/.codex/config.toml`. A project
`.codex/config.toml` can be used for a trusted project when the configuration
should travel with that project. Keep the URL private when the Agent Context
host is running without authentication.

After registering the server, use Codex's MCP view (`/mcp` in the TUI) to
confirm that the server is available and exposes these five tools:

| Tool | Use |
|---|---|
| `save_session` | Report meaningful work and its structured outcome |
| `search_memory` | Search domain-scoped Knowledge before starting familiar work |
| `find_similar_solution` | Look for a reusable recorded solution |
| `get_skill` | Load a centrally managed Skill package |
| `rate_knowledge` | Mark retrieved Knowledge useful or not useful |

## Use the shared Skill

The canonical client-neutral Skill package is
[`../skills/agent-context-guide/SKILL.md`](../skills/agent-context-guide/SKILL.md).
When it has been published to the Agent Context instance, load it through MCP:

```text
get_skill(domain="dev", slug="agent-context-guide")
```

The package teaches the reporting and retrieval loop. In particular:

- search existing Knowledge before reinventing a solution;
- report one structured Session after meaningful work;
- include known model and token usage when available;
- store full context only when the user explicitly asks to remember it; and
- rate Knowledge that was genuinely useful or misleading.

This remote Skill package is separate from Codex's local configuration. The
Agent Context package is the shared source of guidance for every connected
client.

## Remote deployments

Replace the local URL with the reachable HTTPS endpoint, for example
`https://agent-context.example.com/mcp`. Use an authenticated private proxy or
another access control layer before exposing the unauthenticated MVP endpoint
outside a trusted network.

For Codex MCP configuration details, see the official
[MCP guide](https://learn.chatgpt.com/docs/extend/mcp). For the Skill format,
see the official [Skills guide](https://learn.chatgpt.com/docs/build-skills).
