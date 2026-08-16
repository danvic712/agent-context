# T12 redesign — Field Notes UI + Skill folder tree: verification (2026-08-16)

Follow-up to T12 (issue #13, after user design review): the UI was redesigned to
the **Field Notes「手账 × AI」** direction — day palette = graphite × electric blue
(`#eef0f2` paper, `#2563eb` primary), night palette = indigo night × coral
(`#131a28` paper, `#ff7266` primary); Fraunces display + Caveat scribble +
Instrument Sans + JetBrains Mono. The skill package is presented as a **folder
tree** with subfolder support (scripts/, reference/, …). Responsive from 1440px
down to mobile.

## Verification points (all passed)

| # | Scenario | Result |
|---|---|---|
| 1 | Field Notes shell renders: numbered sidebar nav (01 知识…07 设置), paper ruled-line texture (`repeating-linear-gradient` on body), Fraunces brand, scribble accents | ✓ |
| 2 | Theme switch to dark persists `settings.theme=dark` (DB) and applies night palette (`--paper #131a28`) | ✓ |
| 3 | Skill package opens as a **folder tree** (left) + markdown body (right); `SKILL.md` on top | ✓ |
| 4 | **New folder** (scripts) → writes `scripts/.gitkeep`; tree shows the folder and **hides .gitkeep** | ✓ |
| 5 | **New file** `scripts/helper.sh` → auto-creates the subdirectory; tree shows `scripts ▾ 1 → helper.sh` | ✓ |
| 6 | Rename / delete row actions present (read→write→delete composed via existing REST) | ✓ (code) |
| 7 | Responsive: at 700px the sidebar collapses and a top tab strip appears; <lg the tree stacks above the body; KPI grid 2-up | ✓ |
| 8 | Markdown + shiki highlighting still render under the new system | ✓ |
| 9 | i18n en/zh keys for tree actions (newFile/newFolder/rename/packageTree/invalidPath…) resolve | ✓ |

## Follow-up: botanical theme (798e087 → 13cfd56)

User design review replaced Field Notes with the **柔和植物 (Botanical)** direction:

- **Top navigation** replaces the left sidebar: brand mark + pill tabs + engine
  status + theme toggle; nav scrolls horizontally on small screens (verified at
  800/700px).
- **Palette**: day = mist-blue × amber (`#eef2f6` paper, `#2f6fc8` primary,
  `#d98e4a` accent) and night = night-sky blue (`#15202b` paper, `#84c3ec`
  primary) — variables only, both themes same-level. Newsreader display font.
- Settings page fills the viewport (theme/language side by side on lg, LLM form
  full-width); code blocks stay readable in both themes (light shiki tokens on
  the always-dark code panel).
- **Code review fixes** (13cfd56): folder rename (recursive, byte-exact via
  Blob), new-file-inside-folder (pre-filled path), shared path validation
  (rejects traversal + `.gitkeep` names), tree a11y (role=tree/treeitem,
  keyboard), dead `.scribe` class removed.

Verified in-browser: folder rename `scripts → scripts-v2` migrated every child
(`.gitkeep`, `helper.sh`); new file `scripts-v2/backup.sh`; `.gitkeep` name
rejected with the friendly inline error. Backend 173/173 tests green.

## Notes

- Browser automation: `fill` does not reliably trigger React onChange on the
  tree's controlled inputs — native value setter + `input` event, then a
  `dispatchEvent(new KeyboardEvent('keydown', {key:'Enter'}))` is required (the
  tool's synthetic `key Enter` did not always reach React). Not a product issue.
- Backend untouched: the folder tree is client-side over the existing manifest
  (paths already carry `/`); file/folder creation uses the T12 `PUT file` seam.
- Day/night palettes are CSS variables only — `settings.theme` + `[data-theme]`
  mechanism unchanged (T12 theme work intact).
- Design exploration prototyped in `docs/design/` (ui-blueprint, ui-editorial,
  ui-directions-compare, ui-notebook-ai, ui-notebook-palettes); the chosen
  direction is Field Notes.
