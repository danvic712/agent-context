using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Domain;
using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AgentContext.Application.Hygiene;

/// <inheritdoc cref="IEngineHealthAppService"/>
/// <summary>
/// Learning Engine health view (T8 / spec US29): reads the Postgres-as-queue
/// sessions table (ADR 0005) for queue depth, failures and retry visibility.
/// </summary>
public sealed class EngineHealthAppService(AgentContextDbContext db) : IEngineHealthAppService
{
    public async Task<EngineHealthDto> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var counts = await db.Sessions.AsNoTracking()
            .GroupBy(s => s.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.Key, g => g.Count, cancellationToken);

        var retryScheduled = await db.Sessions.AsNoTracking()
            .CountAsync(s => s.Status == SessionStatus.Failed && s.NextAttemptAtUtc != null, cancellationToken);
        var total = counts.Values.Sum();

        return new EngineHealthDto(
            QueuedSessions: counts.GetValueOrDefault(SessionStatus.Pending),
            ProcessingSessions: counts.GetValueOrDefault(SessionStatus.Processing),
            FailedSessions: counts.GetValueOrDefault(SessionStatus.Failed),
            RetryScheduledSessions: retryScheduled,
            TotalSessions: total);
    }
}
