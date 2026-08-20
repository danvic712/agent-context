namespace AgentContext.Application.Dtos;

/// <summary>
/// A bounded page of the latest installed Skill versions. The cursor is an
/// opaque continuation token owned by the application service.
/// </summary>
public sealed record SkillListPage(
    int PageSize,
    string? Cursor,
    IReadOnlyList<SkillListItem> Items,
    bool HasMore,
    string? NextCursor);
