using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace AgentContext.Host.Controllers;

/// <summary>
/// Knowledge retrieval surface (issue #5 / spec US 17/18): thin adapter over
/// <see cref="IRetrievalAppService"/> — search_memory and find_similar_solution
/// share one retrieval backend. Knowledge CRUD + review/delete land with T5.
/// </summary>
[ApiController]
[Route("api/knowledge")]
public sealed class KnowledgeController(IRetrievalAppService retrieval) : ControllerBase
{
    /// <summary>search_memory: domain-scoped semantic Knowledge retrieval.</summary>
    [HttpGet("search")]
    public async Task<ActionResult<SearchMemoryResult>> Search(
        [FromQuery] string domain,
        [FromQuery] string q,
        [FromQuery] double? minConfidence = null,
        CancellationToken cancellationToken = default)
        => Ok(await retrieval.SearchMemoryAsync(domain, q, minConfidence, cancellationToken));

    /// <summary>find_similar_solution: best-matching historical Solution for a problem.</summary>
    [HttpGet("similar-solution")]
    public async Task<ActionResult<FindSimilarSolutionResult>> FindSimilarSolution(
        [FromQuery] string domain,
        [FromQuery] string problem,
        CancellationToken cancellationToken = default)
        => Ok(await retrieval.FindSimilarSolutionAsync(domain, problem, cancellationToken));
}
