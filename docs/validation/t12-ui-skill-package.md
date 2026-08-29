# T12 — Direction D UI + theme (DB) + Skill packages: UI verification (2026-08-16)

Manual browser verification of the T12 work (issue #13): product-grade Direction D
UI refactor, the DB-backed platform theme (`settings.theme`, like the language),
skeletons/animations, and the filesystem Skill package model with markdown
rendering (shiki), file editing, upload and zip import.

## Environment

- `docker compose stop portal` → local `AgentContext.Host` (8080) + `npm run dev`
  (5173) → in-app Chromium → restored `docker compose start portal` after.
- The DB already had a wizard-created user; `language=zh-CN`; theme untouched
  (defaults to `system`).

## Verification points (all passed)

| # | Scenario | Result |
|---|---|---|
| 1 | Direction D shell renders: sidebar (AC logo + 7 nav items), topbar engine pill ("正常/healthy"), content area | ✓ |
| 2 | Theme quick toggle in topbar: click dark → `data-theme=dark`, **DB `settings.theme=dark`**, localStorage synced | ✓ |
| 3 | Reload → theme stays dark (DB is authoritative, localStorage first-paint cache) | ✓ |
| 4 | Settings page theme selector (Light/Dark/System radio group); picking System persists `system` and resolves via matchMedia | ✓ |
| 5 | Skills page: left list (name + domain/slug + version badge) + right detail; friendly empty states ("还没有技能…", "在左侧选择一个技能…") | ✓ |
| 6 | Create skill → package lands on disk (`skills/dev/sql-debugging/v1/SKILL.md`), manifest shows SKILL.md | ✓ |
| 7 | SKILL.md renders as markdown (h1/h2 separation) with **shiki syntax-highlighted code block** (15 tokens); theme switch recolors tokens via `--shiki-*` variables | ✓ |
| 8 | File edit mode (Edit → textarea → Save) writes real newlines to disk; "已保存 ✓" notice | ✓ |
| 9 | Publish version: form prefills name/description/**current SKILL.md content**; publishes v2 with a fresh package directory (`v2/SKILL.md`), history kept | ✓ |
| 10 | Upload / Import buttons present with drag-drop hint; zip import + multipart upload covered by REST tests | ✓ |
| 11 | Language switch regression (T11): settings dropdown en↔zh re-renders the whole UI including the new theme copy ("Platform-wide appearance…") | ✓ |
| 12 | Engine/analytics pages render under the new system (Chinese badges, no leftover legacy styling) | ✓ |
| 13 | Skeletons replace "Loading…" on every data view (knowledge/skills/analytics/engine/settings + skill detail); page transitions animate in | ✓ (code-level + structure) |

## Notes

- Backend: 173/173 tests green (153 + 20: package store CRUD/binary/traversal/limit,
  lazy migration Instructions→SKILL.md, publish copies the whole package, file REST,
  zip import, get_skill manifest + `skill://{domain}/{slug}/{file}` resource,
  theme round-trip + invalid 400).
- Skills data directory is `Skills:Directory` (default `skills/`, gitignored);
  compose mounts the single `data` volume at `/data` (T15 follow-up: db + skills
  share it — postgres `PGDATA=/data/agent-context/postgres`, skills
  `Skills__Directory=/data/agent-context/skills`).
- Dockerfile now copies `src/AgentContext.Application/locales/` (the embedded localization store, ADR 0008) into
  the build context — the image could not build before this fix.
- Browser automation cannot drive the native file picker or `confirm()` dialog;
  upload/import/delete are verified at the REST seam and by UI presence.
