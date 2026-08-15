# Self-hosted deployment comes first; SaaS is a later option, not a launch constraint

Agent Context ships as a self-hostable Docker Compose stack (Postgres, Redis, API, UI) before any hosted offering. Multi-tenant SaaS machinery (billing, SLA, org onboarding) is deferred until the core loop is validated.

Why: developer-tool users mistrust cloud-hosted session and memory storage; `docker compose up` is the lowest trust cost. It also keeps the first release free of multi-tenancy complexity.

The `workspace` concept in the data model is the seam where tenants can later be layered.
