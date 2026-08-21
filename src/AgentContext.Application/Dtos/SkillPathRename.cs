namespace AgentContext.Application.Dtos;

/// <summary>A staged file or folder rename inside a Skill package.</summary>
public sealed record SkillPathRename(string From, string To);
