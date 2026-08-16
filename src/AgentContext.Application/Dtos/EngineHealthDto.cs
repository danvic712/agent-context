namespace AgentContext.Application.Dtos;

/// <summary>
/// Learning Engine health view (T8 / spec US29): queue depth, failures and
/// retries, read straight from the Sessions table (Postgres-as-queue, ADR 0005).
/// </summary>
public sealed record EngineHealthDto(
    int QueuedSessions,
    int ProcessingSessions,
    int FailedSessions,
    int RetryScheduledSessions,
    int TotalSessions);
