namespace AgentContext.Application.Dtos;

/// <summary>
/// A Skill package resolved for agent consumption (T12, MCP get_skill): the
/// metadata plus the full file manifest and every file's content — text files as
/// plain content, binary files as base64 with <c>Binary = true</c>.
/// </summary>
public sealed record SkillPackage(
    Guid Id,
    string DomainName,
    string Slug,
    string Name,
    string Description,
    int Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<SkillFileInfo> Manifest,
    IReadOnlyList<SkillFileContent> Files,
    IReadOnlyList<string>? Folders = null);
