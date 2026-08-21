namespace AgentContext.Application.Dtos;

/// <summary>
/// Filters and ordering for the installed Skill library. The cursor is scoped
/// to these values so changing a filter cannot accidentally continue an older
/// result set.
/// </summary>
public sealed record SkillListQuery(
    int? PageSize = null,
    string? Cursor = null,
    string? Search = null,
    string? Domain = null,
    string? SourceType = null,
    string? Sort = null);
