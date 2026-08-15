# MVP background processing: BackgroundService over Postgres-as-queue; Hangfire deferred

The MVP learning pipeline (session → knowledge) and hygiene cleanup run on a plain ASP.NET Core `BackgroundService`. `save_session` writes the session row immediately (`status = pending`); the worker polls Postgres for pending sessions, processes, and updates status (with `next_attempt_at` for retries). Cleanup runs on a `PeriodicTimer`. Hangfire is not in the MVP stack.

Why: the MVP has one queue, one consumer, and one recurring job. Hangfire plus its Postgres storage is a dependency for machinery we will not use yet, and a self-hosted stack should stay small. Postgres-as-queue gives durability across restarts with zero extra moving parts.

Consequence: retries are hand-rolled and there is no job dashboard. Revisit Hangfire when job types, scheduling, or observability demands grow — the worker interface is the swap seam.
