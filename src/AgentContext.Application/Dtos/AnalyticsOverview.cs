namespace AgentContext.Application.Dtos;

/// <summary>
/// The token-only session overview report: sessions and token totals with
/// breakdowns by domain and by agent.
/// </summary>
public sealed record AnalyticsOverview(
    int TotalSessions,
    long TotalTokensIn,
    long TotalTokensOut,
    IReadOnlyList<AnalyticsGroupItem> ByDomain,
    IReadOnlyList<AnalyticsGroupItem> ByAgent);
