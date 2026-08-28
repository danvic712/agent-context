using System.ComponentModel;
using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using ModelContextProtocol.Server;

namespace AgentContext.Host.Mcp;

/// <summary>
/// The v1 toolset's knowledge tools (spec §6.1 / issues #5 + #6): retrieval
/// (search_memory / find_similar_solution) shares one backend, and
/// rate_knowledge drives the dynamic Confidence feedback loop. Errors are
/// localized (T11) through the shared locales app service.
/// </summary>
[McpServerToolType]
public sealed class KnowledgeTools(
    IRetrievalAppService retrieval,
    IKnowledgeAppService knowledge,
    ISettingsAppService settings,
    ILocalesAppService locales)
{
    [McpServerTool(Name = "search_memory")]
    [Description("Searches the platform's Knowledge within a domain, ranked by semantic relevance to the query, excluding items below the Confidence threshold (default 0.5). Conflicting pairs are returned together so both sides are visible.")]
    public async Task<SearchMemoryResult> SearchMemory(
        [Description("Domain to search, e.g. \"dev\" or \"home\".")] string domain,
        [Description("Natural-language query describing what to retrieve.")] string query,
        [Description("Minimum Confidence threshold (default 0.5).")] double? minConfidence = null,
        CancellationToken cancellationToken = default)
        => await McpErrorLocalizer.ExecuteAsync(settings, locales, () =>
            retrieval.SearchMemoryAsync(domain, query, minConfidence, cancellationToken), cancellationToken);

    [McpServerTool(Name = "find_similar_solution")]
    [Description("Returns the best-matching historical Solution Knowledge for a problem description (with conflicting Solutions side by side), or null when none meets the Confidence threshold.")]
    public async Task<FindSimilarSolutionResult> FindSimilarSolution(
        [Description("Domain to search, e.g. \"dev\" or \"home\".")] string domain,
        [Description("Problem description to match against recorded Solutions.")] string problem,
        CancellationToken cancellationToken = default)
        => await McpErrorLocalizer.ExecuteAsync(settings, locales, () =>
            retrieval.FindSimilarSolutionAsync(domain, problem, cancellationToken), cancellationToken);

    [McpServerTool(Name = "rate_knowledge")]
    [Description("Rates a Knowledge item to adjust its Confidence: useful confirms it (+0.1, capped at 1.0); not useful clears it (Confidence 0, item moves to the review list).")]
    public async Task<RateKnowledgeResult> RateKnowledge(
        [Description("Id of the Knowledge item to rate.")] Guid id,
        [Description("true = useful / confirm, false = not useful / clear.")] bool useful,
        CancellationToken cancellationToken = default)
        => await McpErrorLocalizer.ExecuteAsync(settings, locales, () =>
            knowledge.RateAsync(id, useful, cancellationToken), cancellationToken);
}
