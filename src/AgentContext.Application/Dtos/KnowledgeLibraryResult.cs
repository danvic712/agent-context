namespace AgentContext.Application.Dtos;

/// <summary>A bounded Knowledge library page with an opaque continuation cursor.</summary>
public sealed record KnowledgeLibraryResult(
    int Limit,
    string? Cursor,
    IReadOnlyList<KnowledgeListItem> Items,
    bool HasMore,
    string? NextCursor,
    KnowledgeLibraryCounts Counts,
    double ReviewThreshold);
