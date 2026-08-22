using AgentContext.Domain;

namespace AgentContext.Application.Dtos;

/// <summary>
/// Filters and continuation state for the Knowledge management library.
/// Cursor values are scoped to the normalized status and search query.
/// </summary>
public sealed record KnowledgeLibraryQuery(
    KnowledgeStatus? Status = null,
    int? Limit = null,
    string? Cursor = null,
    string? Search = null);
