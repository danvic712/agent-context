# Shared Skills

This directory contains portable `SKILL.md` packages intended to be published
to an Agent Context instance and loaded by connected MCP clients.

## Package conventions

Each package has a stable name and description in YAML frontmatter and may
include assets or scripts alongside `SKILL.md`. In the running platform,
packages are versioned by `(domain, slug)` and stored under the configured
`Skills__Directory` data volume.

The package is loaded through the MCP tool:

```text
get_skill(domain="dev", slug="agent-context-guide")
```

Review a Skill's instructions before publishing it. A Skill is executable
guidance for an agent, not merely a README, and should not contain secrets or
instructions to expose private data.

## Available package

- [`agent-context-guide/SKILL.md`](agent-context-guide/SKILL.md) — shared
  reporting, retrieval, Skill-loading, and Knowledge-feedback workflow for
  Codex and Craft Agents.
