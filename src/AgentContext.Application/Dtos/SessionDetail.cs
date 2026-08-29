namespace AgentContext.Application.Dtos;

/// <summary>Full Session view including reported Skills and Usage.</summary>
public sealed record SessionDetail(
    Guid Id,
    Guid WorkspaceId,
    string? DomainName,
    string AgentName,
    string Task,
    string Conclusion,
    string SummaryJson,
    IReadOnlyList<string> SkillsUsed,
    string Status,
    bool Remembered,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<SessionUsageDto> Usage);
