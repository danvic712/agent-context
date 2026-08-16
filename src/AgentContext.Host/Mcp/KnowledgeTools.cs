using System.ComponentModel;
using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using ModelContextProtocol.Server;

namespace AgentContext.Host.Mcp;

/// <summary>
/// The v1 toolset's retrieval tools (spec §6.1 / issue #5): search_memory and
/// find_similar_solution share one retrieval backend (IRetrievalAppService).
/// </summary>
[McpServerToolType]
public sealed class KnowledgeTools(IRetrievalAppService retrieval)
{
    [McpServerTool(Name = "search_memory")]
    [Description("Searches the platform's Knowledge within a domain, ranked by semantic relevance to the query, excluding items below the Confidence threshold (default 0.5). Conflicting pairs are returned together so both sides are visible.")]
    public async Task<SearchMemoryResult> SearchMemory(
        [Description("Domain to search, e.g. \"dev\" or \"home\".")] string domain,
        [Description("Natural-language query describing what to retrieve.")] string query,
        [Description("Minimum Confidence threshold (default 0.5).")] double? minConfidence = null,
        CancellationToken cancellationToken = default)
        => await retrieval.SearchMemoryAsync(domain, query, minConfidence, cancellationToken);

    [McpServerTool(Name = "find_similar_solution")]
    [Description("Returns the best-matching historical Solution Knowledge for a problem description (with conflicting Solutions side by side), or null when none meets the Confidence threshold.")]
    public async Task<FindSimilarSolutionResult> FindSimilarSolution(
        [Description("Domain to search, e.g. \"dev\" or \"home\".")] string domain,
        [Description("Problem description to match against recorded Solutions.")] string problem,
        CancellationToken cancellationToken = default)
        => await retrieval.FindSimilarSolutionAsync(domain, problem, cancellationToken);
}
