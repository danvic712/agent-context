using AgentContext.Domain;
using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;
using AgentContext.Application.Contracts;

namespace AgentContext.Application.Sessions;

/// <inheritdoc cref="ISessionProcessingAppService"/>
public sealed class SessionProcessingAppService(AgentContextDbContext db) : ISessionProcessingAppService
{
    /// <summary>
    /// Marks every pending session Completed with a single atomic SQL UPDATE
    /// (Postgres-as-queue, ADR 0005): crash-safe, no in-memory queue, and safe
    /// under concurrent workers — each UPDATE claims what it updates.
    /// </summary>
    public async Task<int> MarkProcessedAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        return await db.Sessions
            .Where(s => s.Status == SessionStatus.Pending)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Status, SessionStatus.Completed)
                .SetProperty(s => s.ProcessedAtUtc, now), cancellationToken);
    }
}
