namespace AgentContext.Application.Dtos;

/// <summary>
/// Skill row for the management UI (T6): the latest published version of each
/// (domain, slug), with the current version number so history is visible.
/// </summary>
public sealed record SkillListItem(
    Guid Id,
    string DomainName,
    string Slug,
    string Name,
    string Description,
    int Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    string? SourceType = null);
