namespace AgentContext.Application.Dtos;

/// <summary>Full session view including Usage (spec §6.2: overview data queryable from Usage).</summary>
public sealed record SessionDetail(
    Guid Id,
    Guid WorkspaceId,
    string? DomainName,
    string AgentName,
    string Task,
    string Conclusion,
    string SummaryJson,
    string Status,
    bool Remembered,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<SessionUsageDto> Usage);
