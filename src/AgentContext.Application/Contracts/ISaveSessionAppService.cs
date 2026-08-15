namespace AgentContext.Application.Contracts;

/// <summary>
/// Session recording service (T2): persists a reported Session with Usage
/// attached, resolves/creates the domain tag, honours the remember flag, and
/// exposes the recorded data for the session overview. Primary test seam.
/// </summary>
public interface ISaveSessionAppService
{
    Task<SaveSessionResult> SaveAsync(SaveSessionRequest request, CancellationToken cancellationToken = default);

    Task<SessionDetail> GetAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SessionListItem>> ListAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Postgres-as-queue processing (ADR 0005): marks pending Sessions as processed.
/// Called periodically by <c>SessionProcessingWorker</c>; crash-safe because the
/// queue state lives in the database, not in memory.
/// </summary>
public interface ISessionProcessingAppService
{
    /// <summary>Marks all currently-pending sessions Completed. Returns how many were processed.</summary>
    Task<int> MarkProcessedAsync(CancellationToken cancellationToken = default);
}
