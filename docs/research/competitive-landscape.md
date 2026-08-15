# Competitive Landscape — AI Agent Shared Context / Memory Platforms

**As of: August 2026** · Scope: the "shared context / memory layer for AI agents" category.

Every claim below is sourced from a primary source (official docs, GitHub repositories, first-party API references). No secondary write-ups are used. Citations are inline as `[S1]`, `[S2]`, … with a source list at the bottom.

---

## TL;DR

The category splits into three distinct sub-segments:

1. **Embeddable memory layers** (facts/vectors/graphs you plug into your own agents): Mem0, Zep/Graphiti, Cognee, LangMem/LangGraph.
2. **Stateful agent frameworks** (the agent lives *in* the platform): Letta (MemGPT).
3. **First-party, walled-garden context** (memory tied to one assistant/ecosystem): OpenAI ChatGPT Memory/Projects, Anthropic memory tool + Claude Code skills/plugins.

The most consistently under-served dimensions across all players are **usage/cost analytics** and **cross-platform team governance** — which happens to be the core positioning of this project (Agent Context).

---

## 1. Mem0

**What it is:** "Universal memory layer for AI Agents" — an embeddable long-term memory API/SDK. YC S24, Apache-2.0, ~63.3k GitHub stars. `[S1]`

**Who it's for:** Developers building AI assistants, customer-support chatbots, healthcare/co-pilot apps that need persistent, personalized memory across sessions. `[S1]`

**How it ingests session data:**
- You send raw messages/conversations to `memory.add(messages, user_id=...)`; Mem0 distills them into facts and links entities across memories; at query time `memory.search()` returns only relevant memories. `[S1]` `[S2]`
- The April 2026 algorithm is **single-pass ADD-only extraction** (one LLM call, no UPDATE/DELETE — memories accumulate), with **entity linking** and **multi-signal retrieval** (semantic + BM25 keyword + entity matching, fused) plus **temporal reasoning**. `[S1]`
- Multi-level memory scoping: User, Session, and Agent state. `[S1]`
- Defaults to OpenAI `gpt-5-mini` for extraction and `text-embedding-3-small` for embeddings; other LLMs/embeddings supported. `[S1]`

**Deployment modes:** library (`pip install mem0ai` / npm), self-hosted server (`docker compose up`), or managed Mem0 Platform (zero-ops, audit logs, workspace governance). `[S1]` `[S2]`

**Strengths:** broad SDK/integration surface (LangGraph, CrewAI, browser extension, CLI, agent skills for Claude Code/Codex/Cursor/Windsurf); token-efficient extraction; enterprise governance in the paid platform. `[S1]`

**Weaknesses / gaps:**
- It is a **fact/vector store, not a full context platform** — no skill management, no workflow management, no built-in session analytics in the OSS tier. `[S1]`
- Proprietary optimizations (rerankers, benchmark gains) are **platform-only**; the OSS SDK is directionally similar but not identical. `[S1]`
- Fact-extraction-centric memory can lose episodic/relational structure compared to graph approaches (see Zep/Graphiti). `[S3]`

---

## 2. Zep / Graphiti

**What it is:** Graphiti is the open-source **temporal context graph** engine; Zep is the managed "context infrastructure" built on it. Graphiti ~29.9k stars, Apache-2.0. `[S3]`

**Who it's for:** Teams/enterprises needing governed, low-latency agent memory at scale; Graphiti for those who want a self-hosted OSS core. `[S3]`

**How it ingests session data:**
- A **Context Graph** of entities (nodes), facts/relationships (edges with **temporal validity windows**), **episodes** (raw provenance), and custom ontology types (Pydantic). Each fact has a validity window; outdated facts are *invalidated, not deleted*. `[S3]` `[S4]`
- Ingest via `graph.add` / `thread.add_messages`; retrieval via `graph.search` / `thread.get_user_context`, with hybrid retrieval (semantic + BM25 + graph traversal). `[S4]`
- Zep produces a **Context Block** — an optimized string of user summary + relevant facts — to feed the LLM; sub-200ms retrieval. `[S4]`

**Deployment:** Graphiti self-hosted (Neo4j / FalkorDB / Amazon Neptune / Kuzu-deprecated) with Docker; Zep managed or in-your-cloud. `[S3]`

**Strengths:** temporal reasoning and fact invalidation are best-in-class; provenance (episodes) gives full lineage; MCP server ships in-repo; Zep adds governance, dashboard, SDKs (Python/TS/Go), SLAs. `[S3]` `[S4]`

**Weaknesses / gaps:**
- Graphiti alone is **not turnkey**: you bring your own graph DB and must build user/thread management, retrieval tuning, and tooling yourself (explicitly documented in the "Zep vs Graphiti" table). `[S3]`
- No skill/workflow management; no usage/cost analytics in OSS. `[S3]`

---

## 3. Letta (formerly MemGPT)

**What it is:** "Platform for stateful agents: AI with advanced memory that can learn and self-improve over time." ~24.3k stars, Apache-2.0. `[S5]`

**Who it's for:** Developers building coding agents, personal assistants, "AI coworkers" that *run as* stateful agents (CLI, desktop, Slack/Telegram/Discord channels). `[S6]`

**How it ingests session data:**
- Agents use **MemFS**, a git-backed memory filesystem they can inspect and edit; memory is shared across the agent's conversations. `[S7]`
- Explicit teaching (`/remember`), bootstrap (`/init`), and **Dreaming** — background subagents that review recent conversations, consolidate lessons, and update memory (configured via `/sleeptime`). `[S7]`
- Origin (MemGPT) pioneered hierarchical memory and virtual context management. `[S5]`

**Deployment:** Letta Code CLI (local), desktop app, channels; Letta Agent SDK (TypeScript) on Constellation cloud, fully local, or self-hosted App Server. `[S5]` `[S6]`

**Strengths:** true *self-improving* agent loop (dreaming/consolidation); skills + subagents built-in; model-agnostic. `[S6]` `[S7]`

**Weaknesses / gaps:**
- It is an **agent harness**, not a cross-platform context layer — you build agents *on* Letta rather than retrofitting existing agents (Claude Code/Cursor) with shared memory. `[S6]`
- Legacy V1 server is in maintenance; active development moved to `letta-ai/letta-code` (a migration churn signal). `[S5]`

---

## 4. OpenAI ChatGPT Memory / Projects

**What it is:** First-party, consumer/team-scoped memory and workspaces inside ChatGPT (no embeddable memory API for third-party agents).

**How it ingests session data:**
- **Memory** is controlled by two settings: *Reference saved memories* (explicitly asked to remember) and *Reference chat history* (implicit recall from past chats). Users manage/delete via *Settings → Personalization → Memory summary*. `[S8]`
- Under the hood ChatGPT uses a `save_memories` tool that upserts memories as content strings each turn. `[S9]`
- **Projects** are "smart workspaces": group chats + uploaded files (PDFs/spreadsheets/docs/images) + custom instructions; **Project memory** (default vs project-only) remembers chats/files in the project. `[S10]`
- Shared projects (all plans since Oct 2025) give edit/chat access roles, branching chats, and project-only memory isolation. `[S10]`

**Strengths:** zero-ops, deeply integrated UX; shared projects = lightweight team "context hub"; RBAC + Compliance API for Enterprise. `[S10]`

**Weaknesses / gaps:**
- **Walled garden** — memory/projects exist only within ChatGPT; no developer API to give *your* agents this memory, no MCP/SDK ingestion. `[S8]` `[S10]`
- No cross-agent (non-OpenAI) context; no usage/cost analytics; "no list of project memories" (opaque memory, cannot audit). `[S10]`

---

## 5. Anthropic: memory tool, MCP, and Claude Code skills/marketplace

**Memory tool** (Messages API, GA, Claude 4+):
- A client-side tool (`type: "memory_20250818"`, `name: "memory"`) that lets Claude store/retrieve across conversations in a `/memories` directory via file ops: `view`, `create`, `str_replace`, `insert`, `delete`, `rename`. `[S11]`
- Just-in-time retrieval: Claude checks memory before tasks, records progress, reads back on demand — instead of preloading context. The *application* implements storage (per-user dir, DB, cloud). SDK helpers ship (e.g. `BetaLocalFilesystemMemoryTool`). `[S11]`
- Pairs with **context editing** and **compaction**; documented multisession software-development pattern. `[S11]`

**MCP (Model Context Protocol):**
- Open standard (JSON-RPC 2.0) for connecting AI apps to external data/tools/workflows — "a USB-C port for AI applications". Servers expose **Resources, Prompts, Tools**; clients expose sampling/roots/elicitation. `[S12]`
- Anthropic introduced MCP (Nov 2024) and it is now community-governed at modelcontextprotocol.io, supported by Claude, ChatGPT, VS Code, Cursor, and many others. `[S12]` `[S13]`

**Claude Code skills + marketplace ecosystem:**
- **Skills** = `SKILL.md` files (YAML frontmatter + markdown body) following the open **Agent Skills** standard (agentskills.io); loaded automatically or via `/skill-name`; stored at enterprise/personal (`~/.claude/skills/`)/project (`.claude/skills/`)/plugin levels. `[S14]`
- **Plugins** = self-contained dirs bundling skills, agents, hooks, MCP servers, LSP servers, background monitors, with a `.claude-plugin/plugin.json` manifest; distributed via **marketplaces** (official `anthropics/claude-plugins-official` + community `anthropics/claude-plugins-community`). `[S15]`

**Strengths:** the most mature **skill/workflow distribution** story (versioned plugins, marketplaces, namespacing, live reload); memory tool gives developers full storage control. `[S14]` `[S15]`

**Weaknesses / gaps:**
- Memory tool is a **low-level primitive** — no built-in extraction/semantic search/graph, no dedup, no automated distillation; *you* write the handler and storage. `[S11]`
- No managed memory platform, no usage/cost analytics, no team workspace governance (team sharing is via git repos/marketplaces). `[S14]` `[S15]`

---

## 6. Context7, Cognee, LangMem / LangGraph

### Context7 (upstash/context7, ~60.8k stars, MIT)
- **Up-to-date, version-specific code docs injected into prompts** for coding agents (MCP server + `ctx7` CLI + skills). MCP tools: `resolve-library-id`, `query-docs`. `[S16]`
- **Not a memory platform** — it solves "fresh docs/context," not "persistent cross-session memory." Read-only retrieval; no knowledge accumulation, no teams, no analytics. `[S16]`

### Cognee (topoteretes/cognee, ~30k stars, Apache-2.0)
- "Open-source AI memory platform for agents": `remember` / `recall` / `forget` / `improve`; self-hosted **knowledge graph + vector** (cognitive-science-grounded ontology); multimodal ingestion. `[S17]`
- Ships a **Claude Code plugin** (`cognee-memory@cognee`) that captures prompts/tool traces, injects context, and syncs session memory into the permanent graph; plus an MCP server, Cognee Cloud, and Postgres-only memory-layer option. `[S17]`
- Enterprise traits: user/tenant isolation, OTEL collector, audit traits, traceability. `[S17]`

### LangMem / LangGraph memory (langchain-ai/langmem, ~1.6k stars, MIT)
- LangMem = toolkit to **extract/consolidate** memory + **memory tools** (`create_manage_memory_tool`, `create_search_memory_tool`) + a **background memory manager**; native integration with LangGraph's **Long-term Memory Store**. `[S18]`
- LangGraph memory model: short-term = thread-scoped state via checkpointer; long-term = JSON docs in a `BaseStore` under custom `namespace`/`key` with semantic search; semantic/episodic/procedural memory types; "hot path" vs "background" writing. `[S9]`

**Positioning:** these are the **DIY building blocks** of the category — powerful but require assembly, and LangMem is LangChain-ecosystem-bound. `[S18]` `[S9]`

---

## 7. Is "self-host an MCP server" enough? (Evaluation)

**Short answer: no — MCP is a transport standard, not a memory/knowledge layer.**

- MCP standardizes *how* an agent connects to external systems (Resources/Prompts/Tools over JSON-RPC), and explicitly delegates security (consent, data privacy, tool safety) to the implementer — "MCP itself cannot enforce these security principles at the protocol level." `[S12]`
- Therefore a bare self-hosted MCP server gives you **connection**, but none of: session→knowledge extraction pipelines, semantic/graph storage, temporal reasoning, dedup/conflict resolution, multi-agent identity & workspace governance, team sharing/permissions, skill/workflow management, or usage/cost analytics. `[S12]` (and by contrast `[S1]` `[S3]` `[S17]`)
- In practice, the "self-host an MCP server" route almost always means **self-hosting one of the OSS projects above** (Mem0, Graphiti, Cognee, Zep, Context7 all ship MCP servers), which inherits their gaps. `[S1]` `[S3]` `[S16]` `[S17]`
- What a self-hosted MCP server *is* good for: standardized tool/resource exposure to multiple clients (Claude Code, Cursor, VS Code, ChatGPT), so it is the right **integration substrate** for a shared-context platform — but the platform logic (memory, skills, sessions, analytics) still has to be built on top. `[S12]` `[S13]`

---

## 8. Gap Analysis by Dimension

| Dimension | Mem0 | Zep/Graphiti | Letta | OpenAI Mem/Projects | Anthropic mem tool + CC | Cognee | LangMem/LangGraph | Context7 |
|---|---|---|---|---|---|---|---|---|
| Multi-agent cross-platform unified context | Partial (user/agent/session memory via SDK/MCP) | Strong (user/entity-scoped graphs, any client) | Partial (agents live *in* Letta) | Weak (ChatGPT only) | Weak (client-side, per-app) | Strong (cross-agent knowledge sharing) | Partial (LangGraph only) | None (docs only) |
| Session → knowledge automation | Strong (extraction pipeline) | Strong (episodes→facts) | Strong (dreaming) | Yes (save_memories) | Weak (manual, Claude writes files) | Strong (cognify pipeline) | Strong (background manager) | None |
| Skills / workflow central mgmt | Weak (skills only for *its own* adoption) | None | Strong (skills + subagents) | None | **Strong** (skills + plugins + marketplaces) | None | None | Weak (setup skills) |
| Team sharing / governance | Platform-only (workspace governance) | Strong (Zep governance, enterprise) | Partial (channels) | Strong (shared projects + RBAC) | Weak (git/marketplace) | Partial (tenant isolation, audit) | None | None |
| Usage analytics & cost tracking | Platform-only (audit logs) | Partial (dashboard, debug logs) | Partial | None | None | Partial (OTEL, audit traits) | Partial (LangSmith, opt-in) | None |

**Key takeaways:**

1. **Analytics & cost tracking is the biggest whitespace.** Almost nobody (outside Zep's dashboard, Cognee's OTEL, and paid Mem0 platform) tracks token consumption, cost, skill effectiveness, or session outcomes — a core pillar of Agent Context's "Usage Intelligence." `[S2]` `[S3]` `[S17]`

2. **Skill/workflow management is dominated by Claude Code's plugin+marketplace model**, which is the clearest precedent for Agent Context's centralized skill management (versioned, namespaced, team-distributable). Others (Letta) have skills but no distribution marketplace. `[S14]` `[S15]` `[S6]`

3. **Cross-platform unified context is split**: memory *layers* (Mem0, Zep, Cognee) are cross-platform but lack the workspace/governance/analytics layer; *platforms* (OpenAI, Anthropic, Letta) have governance/UX but are walled or single-harness. A cross-platform shared-context layer with governance + analytics is open territory. `[S1]` `[S3]` `[S10]` `[S11]`

4. **Session→knowledge automation is table stakes** now (Mem0 extraction, Graphiti episodes, Cognee cognify, Letta dreaming, LangMem background manager) — any credible entrant must have it; it is not a differentiator on its own. `[S1]` `[S3]` `[S7]` `[S17]` `[S18]`

5. **MCP is the connective tissue, not the product.** Every serious player exposes an MCP server; the durable value is the memory/knowledge/skills/analytics logic *behind* it. `[S12]` `[S1]` `[S3]` `[S16]` `[S17]`

---

## Source List (primary sources)

- `[S1]` Mem0 GitHub README — https://github.com/mem0ai/mem0
- `[S2]` Mem0 Platform overview — https://docs.mem0.ai/platform/overview
- `[S3]` Graphiti GitHub README — https://github.com/getzep/graphiti
- `[S4]` Zep docs (concepts) — https://help.getzep.com/concepts
- `[S5]` Letta GitHub README — https://github.com/letta-ai/letta
- `[S6]` Letta docs (home) — https://docs.letta.com/
- `[S7]` Letta docs — Memory & dreaming — https://docs.letta.com/configuration/memory
- `[S8]` OpenAI Help — "How does Reference saved memories work" — https://help.openai.com/en/articles/11146739-how-does-reference-saved-memories-work
- `[S9]` LangGraph docs — Memory concepts — https://docs.langchain.com/oss/python/concepts/memory
- `[S10]` OpenAI Help — "Projects in ChatGPT" — https://help.openai.com/en/articles/10169521-using-projects-in-chatgpt
- `[S11]` Anthropic docs — Memory tool — https://platform.claude.com/docs/en/agents-and-tools/tool-use/memory-tool
- `[S12]` MCP specification (2025-06-18) — https://modelcontextprotocol.io/specification/2025-06-18/
- `[S13]` MCP introduction — https://modelcontextprotocol.io/introduction
- `[S14]` Claude Code docs — Skills — https://code.claude.com/docs/en/skills
- `[S15]` Claude Code docs — Plugins / marketplaces — https://code.claude.com/docs/en/plugins
- `[S16]` Context7 GitHub README — https://github.com/upstash/context7
- `[S17]` Cognee GitHub README — https://github.com/topoteretes/cognee
- `[S18]` LangMem GitHub README — https://github.com/langchain-ai/langmem
