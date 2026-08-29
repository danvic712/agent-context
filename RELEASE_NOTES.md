# v0.0.3

> Released 2026-08-29 · linux/amd64 + linux/arm64

## Summary

v0.0.3 is a product and platform release focused on a more usable knowledge
workspace, versioned Skill packages, and richer session provenance. It also
keeps the frontend and backend localization resources in one build-owned
location so the two surfaces cannot drift apart.

## Highlights

- **Knowledge workspace.** The library now has a responsive browse-and-review
  workspace with clearer Active / Review / Archived states, contextual detail,
  and improved empty, loading, and error experiences.
- **Skill packages.** Skills support a versioned filesystem package model with
  library and detail views, package file browsing, Monaco editing, language
  detection, upload and ZIP import, immutable publishing, archive downloads,
  and deletion.
- **Session provenance.** `save_session` accepts `skillsUsed`, and the session
  record persists the Skill identifiers used by an agent for later inspection.
- **Setup and inference.** First-run setup can defer provider configuration,
  while platform inference routes and provider validation remain available from
  the settings surface.
- **Localization packaging.** Grouped `en-US` and `zh-CN` JSON resources now
  live under `src/AgentContext.Application/locales/`, are embedded by the
  backend, and are imported by the Vite frontend from the same source tree.

## Engineering

- PostgreSQL persistence mappings and migrations use explicit lowercase
  `snake_case` names for the application schema.
- CI discovers and runs all test projects under `tests/`, with a retry for
  transient Testcontainers failures.
- Docker releases continue to publish the application image for both
  `linux/amd64` and `linux/arm64`, with provenance disabled to keep the GHCR
  image index limited to the real architectures.

## Images

`ghcr.io/danvic712/agent-context` → `latest` + `v0.0.3` (multi-arch).
