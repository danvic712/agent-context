using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AgentContext.Application.Analytics;

/// <inheritdoc cref="IAnalyticsAppService"/>
/// <summary>
/// Token-only session overview analytics (T7 / spec US27): sessions and token
/// totals by domain and agent, optionally filtered.
/// </summary>
public sealed class AnalyticsAppService(AgentContextDbContext db) : IAnalyticsAppService
{
    public async Task<AnalyticsOverview> GetOverviewAsync(
        Guid? workspaceId = null,
        string? domain = null,
        string? agent = null,
        CancellationToken cancellationToken = default)
    {
        var workspaceIdResolved = workspaceId ?? await FirstWorkspaceIdAsync(cancellationToken);
        if (workspaceIdResolved is null)
        {
            return new AnalyticsOverview(0, 0, 0, [], []);
        }

        var sessions = await db.Sessions.AsNoTracking()
            .Where(s => s.WorkspaceId == workspaceIdResolved)
            .Include(s => s.Domain)
            .Include(s => s.Usage)
            .ToListAsync(cancellationToken);

        var rows = sessions
            .Where(s => string.IsNullOrWhiteSpace(domain)
                || string.Equals(s.Domain?.Name, domain.Trim(), StringComparison.OrdinalIgnoreCase))
            .Where(s => string.IsNullOrWhiteSpace(agent)
                || string.Equals(s.AgentName, agent.Trim(), StringComparison.OrdinalIgnoreCase))
            .Select(s => new SessionRow(
                s.Domain?.Name ?? "(none)",
                s.AgentName,
                s.Usage))
            .ToList();

        var totalTokensIn = rows.Sum(r => (long)r.Usage.Sum(u => (long)u.InputTokens));
        var totalTokensOut = rows.Sum(r => (long)r.Usage.Sum(u => (long)u.OutputTokens));

        var byDomain = rows
            .GroupBy(r => r.DomainName)
            .Select(g => GroupItem(g.Key, g.Count(), g.SelectMany(r => r.Usage).ToList()))
            .OrderByDescending(g => g.TokensIn + g.TokensOut)
            .ToList();

        var byAgent = rows
            .GroupBy(r => r.AgentName)
            .Select(g => GroupItem(g.Key, g.Count(), g.SelectMany(r => r.Usage).ToList()))
            .OrderByDescending(g => g.TokensIn + g.TokensOut)
            .ToList();

        return new AnalyticsOverview(
            rows.Count,
            totalTokensIn,
            totalTokensOut,
            byDomain,
            byAgent);
    }

    private static AnalyticsGroupItem GroupItem(
        string name,
        int sessionCount,
        ICollection<Usage> usage)
        => new(
            name,
            sessionCount,
            (long)usage.Sum(u => (long)u.InputTokens),
            (long)usage.Sum(u => (long)u.OutputTokens));

    private async Task<Guid?> FirstWorkspaceIdAsync(CancellationToken cancellationToken)
        => await db.Workspaces.AsNoTracking()
            .OrderBy(w => w.CreatedAtUtc)
            .Select(w => (Guid?)w.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private sealed record SessionRow(string DomainName, string AgentName, ICollection<Usage> Usage);
}
