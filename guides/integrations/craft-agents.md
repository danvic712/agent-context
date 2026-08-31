# Connect Craft Agents

Craft Agents connects to Agent Context over the host's Streamable HTTP MCP
endpoint. URL mode is the supported setup for the current single-entrypoint
host; no local binary or `--mcp-stdio` process is required.

## Start Agent Context

The default local deployment is:

```bash
docker compose up -d
```

This starts the Agent Context UI, REST API, and MCP endpoint on port `8080`,
alongside PostgreSQL with pgvector. Open `http://localhost:8080`, complete the
first-run wizard, and configure both the Chat and Embedding routes.

The MCP URL is:

```text
http://localhost:8080/mcp
```

## Create the Craft Agents source

Create a Craft Agents MCP source with a configuration equivalent to this
`config.json`:

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
    "url": "http://localhost:8080/mcp",
    "authType": "none"
  }
}
```

Point `mcp.url` at the reachable host. For a remote deployment, use its HTTPS
URL, such as `https://agent-context.example.com/mcp`.

The current MVP MCP endpoint has no authentication. Keep it on a trusted local
network or protect it with an authenticated private proxy before exposing it
remotely.

## Optional explore-mode permissions

If the Craft Agents workspace uses an `permissions.json`, read-oriented access
can be granted with:

```json
{
  "allowedMcpPatterns": [
    { "pattern": "search", "comment": "search_memory / find_similar_solution — read-only retrieval" },
    { "pattern": "get", "comment": "get_skill — read-only Skill loading" },
    { "pattern": "rate", "comment": "rate_knowledge — feedback for Knowledge confidence" }
  ]
}
```

The normal agent workflow also needs `save_session` so that meaningful work can
be reported to the shared Learning Engine.

## Verify the connection

Run the Craft Agents source test or activation flow. A successful connection
should expose all five v1 tools:

`save_session`, `search_memory`, `find_similar_solution`, `get_skill`, and
`rate_knowledge`.

If the database is new, complete setup before testing tools. Retrieval and the
Learning Engine also require valid Chat and Embedding routes.

## Troubleshooting

- **Setup error**: open the web UI and complete the first-run wizard.
- **Inference is not configured**: save one validated Chat route and one
  validated Embedding route in Setup or Settings.
- **Skill not found**: check that the Skill package has been published to the
  Agent Context instance and that its domain and slug are correct.
- **MCP invocation error**: inspect the Host logs (`docker logs` for Compose or
  the local terminal for a direct run) for the underlying exception.
