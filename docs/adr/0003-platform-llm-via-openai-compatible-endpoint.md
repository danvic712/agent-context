# Platform-side LLM calls go through a configurable OpenAI-compatible endpoint

The Learning Engine (extraction + embedding) calls an LLM supplied by the workspace setter-upper: an OpenAI-compatible base URL plus an API key. The same endpoint serves both extraction and embedding in v1.

Why: one abstraction covers cloud providers and local runtimes (Ollama, LM Studio, gateways) without multi-SDK sprawl; self-hosters keep the choice of where the model lives. Custom base URL keeps the data-locality decision with the operator.

Consequence: any provider without an OpenAI-compatible surface needs an adapter later; model-pricing analytics derives cost from token counts plus a maintained pricing table, not provider SDKs.

> Superseded by [ADR 0009](./0009-platform-inference-configuration.md), which keeps this original decision as historical context while introducing independent Chat and Embedding routes.
