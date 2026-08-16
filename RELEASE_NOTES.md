# v0.0.2

> Released 2026-08-16 · linux/amd64 + linux/arm64

## Summary

v0.0.2 is a release-hygiene + documentation release on top of the T1–T13 feature
work shipped in v0.0.1. The image is functionally identical to v0.0.1's code but
its multi-arch index is now clean.

## Fixed

- **GHCR image index no longer shows an `unknown/unknown` platform.** Buildx
  defaults to `provenance: true`, appending a SLSA attestation manifest per
  architecture that GitHub Packages renders as a third `unknown/unknown` entry
  alongside `linux/amd64` and `linux/arm64`. Provenance is now disabled
  (`provenance: false`), so the index contains exactly the two real
  architectures. Applies to this tag and all future releases.
- **CI test flakiness.** The build workflow retries the test suite once when a
  Testcontainers container dies mid-run (transient Npgsql connection reset),
  instead of failing the whole pipeline on an infrastructure flake.

## Documentation

- `README.md`, `AGENTS.md`, `docs/spec.md` and `docs/overview.md` synced to the
  delivered state (T1–T13):
  - Three-mode entrypoint — `--web` (REST API + UI), `--mcp-stdio` (Craft
    Agents MCP over stdio), `--apphost` (Aspire dashboard Resources view).
  - Compose now runs portal + Postgres(pgvector) + **Aspire dashboard**;
    prebuilt images ship from GHCR.
  - Skill packages (file-tree management), platform localization (en-US/zh-CN),
    DB-persisted LLM/language/theme settings, OpenTelemetry + dashboard,
    GitHub Actions CI/CD, and the **191/191** test baseline.

## Images

`ghcr.io/danvic712/agent-context` → `latest` + `v0.0.2` (multi-arch).
