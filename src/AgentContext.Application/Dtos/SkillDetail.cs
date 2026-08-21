namespace AgentContext.Application.Dtos;

/// <summary>
/// A full Skill including its package file manifest (T12 / spec US21–23). The
/// package files live on the filesystem; the manifest lists them so clients can
/// read individual files. Returned by get_skill over REST; the MCP tool returns
/// <see cref="SkillPackage"/> (manifest + contents).
/// </summary>
public sealed record SkillDetail(
    Guid Id,
    string DomainName,
    string Slug,
    string Name,
    string Description,
    int Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<SkillFileInfo> Manifest,
    string? SourceType = null);
