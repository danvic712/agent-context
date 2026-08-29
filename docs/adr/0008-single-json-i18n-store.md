# Localization resources live in grouped JSON files under one directory per locale

**Status: accepted; revised 2026-08-28.**

All user-facing strings — frontend UI text, backend error messages, and Learning
Engine prompts — come from the shared locale resource tree:

```text
src/AgentContext.Application/locales/
├── en-US/
│   ├── common.json
│   ├── setup.json
│   ├── inference.json
│   ├── knowledge.json
│   ├── skills.json
│   ├── settings.json
│   ├── errors.json
│   └── prompts.json
└── zh-CN/
    └── the same resource files
```

The grouped files retain the existing dotted key vocabulary. `common.json`
contains global UI and shell strings; `setup.json`, `knowledge.json`,
`skills.json`, and `settings.json` contain their page areas; `inference.json`
contains the Setup/Settings shared form; `errors.json` contains both UI error
page strings and backend error messages; and `prompts.json` contains Learning
Engine prompts. Locale codes remain BCP-47 (`en-US`, `zh-CN`).

The frontend imports the UI resource files as separate i18next namespaces and
uses namespace fallback to resolve the unchanged dotted keys. The backend
embeds every JSON file under each supported locale and searches the separate
resource documents in deterministic order. Neither side creates or maintains a
large aggregate locale JSON file.

Why: the original one-file-per-locale arrangement kept a shared source of truth,
but each file grew with every page and backend surface. Grouping by product area
keeps the source shared while making translation ownership, review, and future
changes manageable. Keeping the existing key paths avoids a needless migration
of every component and backend call site.

Consequence: frontend and backend still release together from the same physical
locale resources, but adding a frontend resource group requires registering its
namespace in the frontend loader. The backend resource scan and embedding rules
must include every supported locale directory. Missing keys continue to fall
back to en-US, then to the raw key.
