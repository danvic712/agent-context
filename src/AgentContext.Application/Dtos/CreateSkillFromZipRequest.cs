namespace AgentContext.Application.Dtos;

/// <summary>Metadata for creating a new Skill from an uploaded ZIP package.</summary>
public sealed record CreateSkillFromZipRequest(
    string Domain,
    string Slug,
    string Name,
    string Description);
