namespace AgentContext.Application.Dtos;

/// <summary>A downloadable archive of a persisted Skill package.</summary>
public sealed record SkillPackageDownload(Stream Content, string FileName);
