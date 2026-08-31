# User Guides

This directory contains the user-facing documentation for connecting an MCP
client to Agent Context and using its shared context capabilities.

## Supported clients

| Client | Guide |
|---|---|
| Codex (desktop, CLI, or IDE) | [`integrations/codex.md`](integrations/codex.md) |
| Craft Agents | [`integrations/craft-agents.md`](integrations/craft-agents.md) |

Both clients connect to the same Streamable HTTP MCP endpoint exposed by the
Agent Context host. The client-specific setup is different, but the tools,
skills, domains, sessions, and knowledge are shared.

## Shared Skill

The portable agent instructions are in
[`skills/agent-context-guide/SKILL.md`](skills/agent-context-guide/SKILL.md).
They are intentionally client-neutral and can be published as an Agent Context
Skill package for both Codex and Craft Agents.

## Start here

1. Start Agent Context with `docker compose up -d`.
2. Open `http://localhost:8080` and complete the first-run setup.
3. Configure one Chat route and one Embedding route in Setup or Settings.
4. Follow the guide for your MCP client.

The MCP endpoint is `http://localhost:8080/mcp` for the default local Compose
deployment. The current MVP endpoint has no authentication, so keep it on a
trusted local network or place it behind an authenticated private proxy before
exposing it remotely.

## Scope of this directory

`guides/` is for users operating or connecting to the product. Project
specification, architecture decisions, validation evidence, and implementation
handoffs remain under [`docs/`](../docs/).
