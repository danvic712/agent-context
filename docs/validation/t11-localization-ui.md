# T11 — Platform localization: UI verification (2026-08-16)

Manual browser verification of the T11 localization work (issue #12): the platform
language lives in `settings.language` (per-call resolution), all UI strings come
from the shared `i18n/{locale}.json` store via react-i18next, and backend errors
render in the configured language.

## Environment

- `docker compose stop portal` → TRUNCATE all tables (fresh first-run state) → local
  `AgentContext.Host` binary (`ASPNETCORE_URLS=http://localhost:8080`,
  `ConnectionStrings__Default` → compose postgres) → `npm run dev` (vite, 5173) →
  in-app Chromium. Restored `docker compose start portal` afterwards.

## Verification points (all passed)

| # | Scenario | Result |
|---|---|---|
| 1 | Fresh DB → wizard opens on the **language step** (default English, "Continue" button) | ✓ |
| 2 | Click 中文 (简体) → `PUT /api/settings/language` + `changeLanguage` → wizard title/description/button immediately in Chinese ("欢迎使用 Agent Context … 继续"), no reload | ✓ |
| 3 | Continue → account step renders in Chinese (显示名称 / 邮箱 / 密码) | ✓ |
| 4 | Fill account → LLM step renders in Chinese (Base URL / API 密钥 / 模型 / 嵌入模型（可选）/ 稍后跳过?) | ✓ |
| 5 | Skip LLM → app shell in Chinese (知识 / 复查 / 已归档 / 技能 / 分析 / 引擎 / 设置) | ✓ |
| 6 | Settings tab → language dropdown shows 中文 (简体); switch to English → **whole UI re-renders in English without a reload** (tabs, LLM endpoint card, "not configured — engine idles", Save) | ✓ |
| 7 | Enter invalid Base URL + Save → English error "Save failed / BaseUrl must be an absolute http(s) URL." (REST `{errorCode, message}` via the global filter) | ✓ |
| 8 | Switch language dropdown back to 中文 → whole UI Chinese again; Save again → Chinese error "保存失败 / BaseUrl 必须是完整的 http(s) URL." | ✓ |
| 9 | Knowledge / Skills / Analytics / Engine tabs all render in Chinese (empty states, badges, hygiene copy) | ✓ |

## REST spot-checks (curl)

- `GET /api/settings/language` before any save → `{"language":"en-US"}` (fallback)
- `PUT /api/settings/language` `{"language":"fr-FR"}` → 400
  `{"errorCode":"settings.unsupportedLanguage","message":"Unsupported language \"fr-FR\". …"}`
- `PUT /api/settings/language` `{"language":"zh-CN"}` → persists; subsequent
  `PUT /api/settings/llm-options` (invalid) → `{"errorCode":"llm.baseUrlInvalid",
  "message":"BaseUrl 必须是完整的 http(s) URL。"}`; back to `en-US` → English message
- `POST /api/setup` invalid email under zh-CN → `{"errorCode":"setup.emailInvalid",
  "message":"需要有效的邮箱地址。"}`

## Notes

- Backend: 153/153 tests green (140 prior + 13 new: TranslationService fallback/args,
  language REST round-trip + invalid 400, error localization per language, extraction
  prompt en/zh assertions, updated seam tests asserting `LocalizedException.ErrorCode`).
- Extraction prompt language is asserted by tests (`LlmClientTests`); no real-LLM call
  was needed for UI verification.
- The database now contains the wizard-created admin user + `language=zh-CN` (left as
  the verification result; harmless — the portal container serves the same DB).
