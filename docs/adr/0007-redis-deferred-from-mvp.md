# Redis is not in the MVP stack

MVP caching, session state, and queueing needs are covered by Postgres (as the queue, ADR 0005) and in-memory structures; Redis is deferred.

Why: the MVP has no real-time features (retrieval is request/response via agent-pushed tools), caching is unnecessary at personal/family scale, and queueing moved to Postgres-as-queue. A lean self-hosted stack should not carry a service it will not use. Docker Compose stays at two services: the app and Postgres.

Consequence: revisit when real-time session visibility, cross-instance caching, or pub/sub needs appear.
