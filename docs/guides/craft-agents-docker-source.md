# Connecting Craft Agents to a Deployed Agent Context over Streamable HTTP

> **Preferred since T14**: the platform serves its MCP toolset natively over
> Streamable HTTP at `/mcp` — Craft Agents connects with a single URL, no local
> binary and no docker command in the source config. The docker-exec stdio
> workaround (pre-T14) is kept at the end as a fallback for older images.

## Why one URL?

The platform hosts **UI + REST API + MCP in one process** (the portal):
`ModelContextProtocol.AspNetCore` maps the v1 toolset at `/mcp` (stateless
Streamable HTTP — the SDK's recommended remote transport, no session affinity).
Running the binary with **no arguments starts the ASP.NET Core Host** — UI,
REST API, and MCP in one process. PostgreSQL is supplied as an external
connection, normally by Docker Compose.

```mermaid
graph LR
    CA[Craft Agents<br/>url mode] -->|"http(s)://host/mcp"| P
    subgraph "one process (portal host)"
        P["/mcp Streamable HTTP<br/>stateless"] --> D[DI container]
        UI[REST API + React UI] --> D
    end
    D --> PG[(Postgres<br/>data:/data/agent-context/postgres)]
    D --> SK[(skills<br/>data:/data/agent-context/skills)]
```

## Run options

| Style | Command | What comes up |
|---|---|---|
| Local / default | `docker compose up -d` | Host (UI+MCP on :8080) + PostgreSQL — db and skills share one `data` volume under `/data/agent-context/` |
| Direct Host | `dotnet run --project src/AgentContext.Host` | ASP.NET Core Host on the configured port; requires a reachable PostgreSQL connection string |

## Craft Agents source — config.json (URL mode)

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

Point `url` at the reachable surface: `http://localhost:8080/mcp` for a local
Compose run, or `https://agent-context.orb.local/mcp` behind the Compose proxy.
No `env` is needed when the Host is configured through Compose.

## Docker image build

The Dockerfile has three stages: Node builds the React UI, the .NET SDK
publishes the Host, and the `aspnet` runtime image runs the Host directly.
BuildKit caches npm and NuGet packages:

```bash
docker compose up -d --build
# or, for an explicit architecture:
docker buildx build --platform linux/arm64 --load -t agent-context:local .
```


## permissions.json — Explore-mode access

```json
{
  "allowedMcpPatterns": [
    { "pattern": "search", "comment": "search_memory / find_similar_solution — read-only retrieval" },
    { "pattern": "get", "comment": "get_skill — read-only skill loading" },
    { "pattern": "rate", "comment": "rate_knowledge — feedback drives Confidence (read-oriented)" }
  ]
}
```

## Prerequisites for a working toolset

- **Setup**: a fresh database needs the first-run wizard first
  (`POST /api/setup`) — `save_session` and friends require a workspace.
- **Inference configuration**: `search_memory`, `find_similar_solution` and the
  Learning Engine need one validated Chat route and one validated Embedding
  route. Configure them through the first-run wizard or Settings, or use
  `PUT /api/inference/configuration` with provider connections and route
  bindings. Test an unsaved draft with
  `POST /api/inference/configuration/verify`. Reads return only masked/key
  configured state; never commit real credentials to source control.

## Validate

Run the Craft Agents `source_test` flow — a successful connection reports all
five v1 tools:

`save_session`, `search_memory`, `find_similar_solution`, `get_skill`, `rate_knowledge`

## Full loop (validated 2026-08-16, URL mode)

1. `source_test` over `http://localhost:8080/mcp` → five tools visible.
2. `save_session(domain="dev", task=…, conclusion=…, keySnippets=[…], skillsUsed=[…], usage={model: …, inputTokens: …, cachedInputTokens: …, outputTokens: …})`
   → Session persisted as `pending`.
3. The in-process Learning Engine extracted → **3 Knowledge rows**,
   Confidence ≈ 0.58–0.59.
4. `search_memory(domain="dev", query="how does Craft Agents connect by URL")`
   → top hit "通过 URL 连接 Streamable HTTP MCP 端点" at **score 0.709**.

## Troubleshooting

- **`save_session` fails on a fresh database** — no workspace yet; run setup first.
- **`search_memory` returns an inference-not-configured error** — both inference
  routes are not saved or no longer resolve to valid protected provider keys;
  configuration is resolved per call, so no restart is needed.
- **`get_skill` returns "skill 不存在"** — empty skills volume / unregistered
  slug; normal data state, not a connection failure.
- **`An error occurred invoking '…'`** — the MCP wrapper swallows inner
  exceptions; real stacks go to the process stderr (`docker logs` in compose,
  the terminal in a local run).
