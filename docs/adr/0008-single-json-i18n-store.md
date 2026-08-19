# Localization resources live in one JSON file per locale, shared by frontend and backend

All user-facing strings — frontend UI text and backend error messages — come from a single JSON store: `i18n/en-US.json` and `i18n/zh-CN.json` at the repo root, namespaced (`ui` for frontend, `errors` for backend). The frontend bundles them via Vite import; the backend loads the same files (embedded resource at build time, same physical source). The platform language is stored in the `settings` table and resolved per call; inference provider and route data use their dedicated tables.

Why: the obvious split would be `.resx` for .NET plus a separate react-i18next dictionary — two stores that drift. One file per locale keeps a single source of truth; changing a string touches exactly one file.

Consequence: backend and frontend release together by construction (same repo, same build); translation keys are shared vocabulary. Locale codes are BCP-47 (`en-US`, `zh-CN`).
