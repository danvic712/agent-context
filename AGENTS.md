# AGENTS.md

A shared context layer for AI agents — manage skills, memory, sessions, and knowledge across personal, family, and team workspaces. Self-hosted, .NET + React, MCP-first.

## Agent skills

### Issue tracker

GitHub Issues, driven through the `gh` CLI (infer repo from `git remote`). See `docs/agents/issue-tracker.md`.

### Triage labels

Default five: `needs-triage`, `needs-info`, `ready-for-agent`, `ready-for-human`, `wontfix`. See `docs/agents/triage-labels.md`.

### Domain docs

Single-context: root `CONTEXT.md` + `docs/adr/`. Read them before exploring. See `docs/agents/domain.md`.

### Coding standards

Root `CODING_STANDARDS.md` documents the repo's C# conventions (notably: interfaces and shared DTOs live in `Contracts` folders, implementations in feature folders).
