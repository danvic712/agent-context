namespace AgentContext.Application.Dtos;

/// <summary>Metadata for creating a new Skill from one uploaded file.</summary>
public sealed record CreateSkillFromFileRequest(
    string Domain,
    string Slug,
    string Name,
    string Description,
    string FileName);
