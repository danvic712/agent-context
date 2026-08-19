# T14 — Inference configuration browser validation

Validated locally on 2026-08-19 against the development portal with an isolated
PostgreSQL database and a local OpenAI-compatible stub. No real provider
credentials were used.

## Browser scenarios

| # | Scenario | Result |
|---|---|---|
| 1 | Settings loads the platform inference configuration from `/api/inference/configuration`. | ✓ |
| 2 | Settings adds a second provider and assigns Chat and Embedding to different provider IDs. | ✓ |
| 3 | Settings sends an unsaved draft to `/api/inference/configuration/verify`; two refused local endpoints produce independent failed checks. | ✓ |
| 4 | Settings keeps “Save configuration” disabled until both checks pass. | ✓ |
| 5 | Setup renders exactly three steps: Account & preferences, Model service, Review & create. | ✓ |
| 6 | Setup verifies Chat Completions and 1536-dimensional Embeddings against the local stub. | ✓ |
| 7 | Review shows the account, language, provider and route summary before creation. | ✓ |
| 8 | Create returns to the application shell and Settings reloads the saved provider and separate Chat/Embedding models. | ✓ |

## Persistence checks

The isolated database contains exactly these new tables:

- `inference_configurations`
- `inference_routes`
- `inference_providers`

The provider row contains a protected `api_key_secret_ref`; the plaintext test
key is not present in the returned REST payload. Route rows contain `Chat` and
`Embedding` bindings and the configured models. No
`inference_configuration_id` reverse foreign key exists on providers.

Automated test code was intentionally not added in this implementation slice;
the existing suite should be extended in the follow-up test ticket.
