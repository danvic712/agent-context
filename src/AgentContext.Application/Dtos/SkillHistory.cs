namespace AgentContext.Application.Dtos;

/// <summary>All retained versions of one Skill lineage, newest first.</summary>
public sealed record SkillHistory(Guid LatestId, IReadOnlyList<SkillVersionSummary> Versions);
