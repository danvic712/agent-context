# Platform inference uses configurable OpenAI-compatible providers and routes

Status: Accepted (2026-08-19), supersedes the single-endpoint model in ADR 0003.

The Learning Engine resolves two platform-level capability routes at runtime:
one Chat Completions route and one Embeddings route. Each route points to a
provider connection and a model, so Chat and Embedding may use different
providers or different models from the same provider.

The configuration is persisted in three PostgreSQL tables:

- `inference_configurations` — platform-level configuration identity and timestamps.
- `inference_routes` — the `Chat` and `Embedding` capability bindings, provider IDs and models.
- `inference_providers` — provider name/type, OpenAI-compatible base URL, and protected API-key secret material. Providers do not contain a reverse configuration foreign key.

The REST surface is deliberately separate from Settings:
`GET/PUT /api/inference/configuration` reads/writes the configuration and
`POST /api/inference/configuration/verify` probes an unsaved draft. The MVP
supports only OpenAI-compatible Chat Completions and Embeddings and requires
1536-dimensional embeddings. Both probes must pass before persistence.

Why: one protocol abstraction covers cloud providers and local runtimes (Ollama,
LM Studio, gateways) without multi-SDK sprawl, while independent routes let
operators choose the best model for each capability. Custom base URLs keep the
data-locality decision with the operator.

Consequence: any provider without an OpenAI-compatible surface needs an adapter
later. The Usage token ledger is retained; the Analytics surface and model-pricing
table are deferred/removed pending redesign. API keys are write-only at the REST boundary and encrypted
before storage.
