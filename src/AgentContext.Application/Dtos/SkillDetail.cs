namespace AgentContext.Application.Dtos;

/// <summary>
/// A full Skill including its markdown instructions (T6 / spec US21–23).
/// Returned by get_skill over REST and MCP; the resource surface
/// skill://{domain}/{slug} resolves to the latest version of the same shape.
/// </summary>
public sealed record SkillDetail(
    Guid Id,
    string DomainName,
    string Slug,
    string Name,
    string Description,
    string Instructions,
    int Version,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
