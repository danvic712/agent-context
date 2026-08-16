namespace AgentContext.Application.Dtos;

/// <summary>
/// The session overview report (spec US27): sessions / tokens / cost, with a
/// top-level total plus breakdowns by domain and by agent. Cost is computed from
/// the maintained pricing table (spec US28) — never client-reported.
/// </summary>
public sealed record AnalyticsOverview(
    int TotalSessions,
    long TotalTokensIn,
    long TotalTokensOut,
    decimal TotalCost,
    IReadOnlyList<AnalyticsGroupItem> ByDomain,
    IReadOnlyList<AnalyticsGroupItem> ByAgent);
