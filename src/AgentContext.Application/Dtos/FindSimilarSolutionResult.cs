namespace AgentContext.Application.Dtos;

/// <summary>
/// find_similar_solution outcome: the best-matching Solution plus its conflict
/// partners (same ConflictGroupId), so both sides are visible for judgement.
/// </summary>
public sealed record FindSimilarSolutionResult(
    KnowledgeSearchItem? Solution,
    IReadOnlyList<KnowledgeSearchItem> Conflicts);
