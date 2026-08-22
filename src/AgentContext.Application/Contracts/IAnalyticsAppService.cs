using AgentContext.Application.Dtos;

namespace AgentContext.Application.Contracts;

/// <summary>
/// Token-only session overview analytics (T7 / spec US27), with breakdowns by
/// domain and agent, optionally filtered.
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
