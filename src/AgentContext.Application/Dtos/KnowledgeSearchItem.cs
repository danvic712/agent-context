using AgentContext.Domain;

namespace AgentContext.Application.Dtos;

/// <summary>
/// One Knowledge item returned by retrieval. <c>Score</c> is the semantic
/// similarity to the query (1 = exact); conflict-group partners appended to a
/// result carry <c>Score = 0</c> to mark them as attached, not ranked.
/// </summary>
public sealed record KnowledgeSearchItem(
    Guid Id,
    KnowledgeType Type,
    string Title,
    string Content,
    double Confidence,
    string? ConflictGroupId,
    bool IsPrivate,
    double Score);
