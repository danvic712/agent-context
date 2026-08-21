namespace AgentContext.Application.Dtos;

/// <summary>Metadata summary for one immutable Skill version.</summary>
public sealed record SkillVersionSummary(
    Guid Id,
    Guid? PreviousVersionId,
    int Version,
    string Name,
    string Description,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string? SourceType,
    bool IsLatest);
