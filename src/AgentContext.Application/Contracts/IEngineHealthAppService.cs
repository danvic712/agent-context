using AgentContext.Application.Dtos;

namespace AgentContext.Application.Contracts;

/// <summary>
/// Learning Engine health view (T8 / spec US29): queue depth, failures and
/// retries from the Postgres-as-queue sessions table (ADR 0005).
/// </summary>
public interface IEngineHealthAppService
{
    /// <summary>Pending/processing/failed/retry-scheduled session counts.</summary>
    Task<EngineHealthDto> GetHealthAsync(CancellationToken cancellationToken = default);
}
