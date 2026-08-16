using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AgentContext.Application.Analytics;

/// <inheritdoc cref="IAnalyticsAppService"/>
/// <summary>
/// Session overview analytics (T7 / spec US27–28): sessions / tokens / cost by
/// domain and agent, optionally filtered. Cost is computed at rollup time from the
/// maintained pricing table — Usage rows carry tokens only, so updating a model's
/// price immediately re-prices the history (US28: "Usage requires no extra
/// collection"). Models without a pricing row cost 0.
/// </summary>
public sealed class AnalyticsAppService(
    AgentContextDbContext db,
    IPricingAppService pricing) : IAnalyticsAppService
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
            return new AnalyticsOverview(0, 0, 0, 0, [], []);
        }

        var rateByModel = await LoadRateByModelAsync(cancellationToken);

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

        var totalTokensIn = rows.Sum(r => (long)r.Usage.Sum(u => (long)u.TokensIn));
        var totalTokensOut = rows.Sum(r => (long)r.Usage.Sum(u => (long)u.TokensOut));
        var totalCost = rows.Sum(r => CostOf(r.Usage, rateByModel));

        var byDomain = rows
            .GroupBy(r => r.DomainName)
            .Select(g => GroupItem(g.Key, g.Count(), g.SelectMany(r => r.Usage).ToList(), rateByModel))
            .OrderByDescending(g => g.Cost)
            .ToList();

        var byAgent = rows
            .GroupBy(r => r.AgentName)
            .Select(g => GroupItem(g.Key, g.Count(), g.SelectMany(r => r.Usage).ToList(), rateByModel))
            .OrderByDescending(g => g.Cost)
            .ToList();

        return new AnalyticsOverview(
            rows.Count,
            totalTokensIn,
            totalTokensOut,
            totalCost,
            byDomain,
            byAgent);
    }

    private async Task<Dictionary<string, (decimal Input, decimal Output)>> LoadRateByModelAsync(
        CancellationToken cancellationToken)
    {
        var prices = await pricing.ListAsync(cancellationToken);
        return prices.ToDictionary(
            p => p.Model,
            p => (p.InputCostPerToken, p.OutputCostPerToken),
            StringComparer.OrdinalIgnoreCase);
    }

    private static AnalyticsGroupItem GroupItem(
        string name,
        int sessionCount,
        ICollection<Usage> usage,
        Dictionary<string, (decimal Input, decimal Output)> rateByModel)
        => new(
            name,
            sessionCount,
            (long)usage.Sum(u => (long)u.TokensIn),
            (long)usage.Sum(u => (long)u.TokensOut),
            CostOf(usage, rateByModel));

    private static decimal CostOf(
        IEnumerable<Usage> usage,
        Dictionary<string, (decimal Input, decimal Output)> rateByModel)
        => usage.Sum(u =>
        {
            if (!rateByModel.TryGetValue(u.Model, out var rate))
            {
                return 0m;
            }

            return u.TokensIn * rate.Input + u.TokensOut * rate.Output;
        });

    private async Task<Guid?> FirstWorkspaceIdAsync(CancellationToken cancellationToken)
        => await db.Workspaces.AsNoTracking()
            .OrderBy(w => w.CreatedAtUtc)
            .Select(w => (Guid?)w.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private sealed record SessionRow(string DomainName, string AgentName, ICollection<Usage> Usage);
}
