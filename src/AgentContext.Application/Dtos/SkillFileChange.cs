namespace AgentContext.Application.Dtos;

/// <summary>A staged file upsert for a new Skill version. Content is always base64 so binary files remain byte-exact.</summary>
public sealed record SkillFileChange(string Path, string ContentBase64);
