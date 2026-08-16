using AgentContext.Application.Dtos;

namespace AgentContext.Application.Contracts;

/// <summary>
/// Shared retrieval backend (spec §6.3 / issue #5): <c>search_memory</c> and
/// <c>find_similar_solution</c> both query the same pgvector Knowledge store —
/// domain-scoped, Confidence-thresholded, conflict pairs surfaced side by side.
/// Exposed over REST and the MCP v1 toolset.
/// </summary>
public interface IRetrievalAppService
{
    /// <summary>
    /// Returns Knowledge from the requested Domain ranked by semantic similarity
    /// to the query, excluding items below <paramref name="minConfidence"/>
    /// (defaults to <c>RetrievalDefaults.MinConfidence</c> = 0.5). Items that are
    /// part of a conflict group bring their group partners along, so both sides
    /// of a contradiction are visible (AC4). Single-user MVP: private items are
    /// always visible to the owner (AC5).
    /// </summary>
    Task<SearchMemoryResult> SearchMemoryAsync(
        string domain, string query, double? minConfidence = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the best-matching historical <c>Solution</c> Knowledge for a
    /// problem description (conflict partners included), or null when no
    /// Solution meets the default Confidence threshold.
    /// </summary>
    Task<FindSimilarSolutionResult> FindSimilarSolutionAsync(
        string domain, string problem, CancellationToken cancellationToken = default);
}
