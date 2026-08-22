using AgentContext.Domain;

namespace AgentContext.Application.Dtos;

/// <summary>
/// Knowledge row for the management UI (issue #6): type, Confidence, private
/// marker, and the source Session's task for provenance.
/// </summary>
public sealed record KnowledgeListItem(
    Guid Id,
    KnowledgeStatus Status,
    KnowledgeType Type,
    string Title,
    string Content,
    double Confidence,
    bool IsPrivate,
    string? DomainName,
    Guid? SourceSessionId,
    string? SourceSessionTask,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    DateTimeOffset? LastUsedAtUtc);
