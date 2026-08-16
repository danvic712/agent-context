using AgentContext.Application.Dtos;

namespace AgentContext.Application.Contracts;

/// <summary>
/// Session overview analytics (T7 / spec US27): sessions / tokens / cost, with
/// breakdowns by domain and agent, optionally filtered. Cost is computed from the
/// maintained pricing table (spec US28) at the rollup level — never client-reported.
/// </summary>
public interface IAnalyticsAppService
{
    /// <summary>
    /// The session overview report. Filters narrow the source sessions before
    /// grouping; all filters are optional. Single-user MVP: an omitted workspace
    /// resolves to the first (only) workspace.
    /// </summary>
    Task<AnalyticsOverview> GetOverviewAsync(
        Guid? workspaceId = null,
        string? domain = null,
        string? agent = null,
        CancellationToken cancellationToken = default);
}
