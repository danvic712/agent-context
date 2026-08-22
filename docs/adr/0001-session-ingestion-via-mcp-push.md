# Sessions are reported by agents over MCP, never captured by a traffic proxy

Agent Context records sessions only when an agent pushes them through the platform's MCP gateway (e.g. `save_session`, event streams), plus per-agent plugins added incrementally. We deliberately do **not** intercept LLM traffic via a proxy.

Why: a proxy would give universal coverage but is invasive, fragile across vendors, and risks violating their terms of service. Push-based reporting keeps ingestion standard (MCP), opt-in, and controllable.

Consequence: any future Analytics surface will only cover agents that actually integrate with the platform. The "track every agent uniformly" expectation from the original overview is dropped.
