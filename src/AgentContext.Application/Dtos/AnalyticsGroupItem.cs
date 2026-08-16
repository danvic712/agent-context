namespace AgentContext.Application.Dtos;

/// <summary>One grouped row of the session overview (by domain or by agent).</summary>
public sealed record AnalyticsGroupItem(
    string Name,
    int Sessions,
    long TokensIn,
    long TokensOut,
    decimal Cost);
