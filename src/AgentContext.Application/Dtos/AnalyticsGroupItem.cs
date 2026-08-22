namespace AgentContext.Application.Dtos;

/// <summary>One token-only grouped row of the session overview.</summary>
public sealed record AnalyticsGroupItem(
    string Name,
    int Sessions,
    long TokensIn,
    long TokensOut);
