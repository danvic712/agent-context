using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace AgentContext.Host.Controllers;

/// <summary>
/// Knowledge surface (issue #5 + #6): retrieval (search_memory /
/// find_similar_solution) and management (list, review, private marker,
/// delete, rate_knowledge). Thin adapters over the application seams.
/// </summary>
[ApiController]
[Route("api/knowledge")]
public sealed class KnowledgeController(
    IRetrievalAppService retrieval,
    IKnowledgeAppService knowledge) : ControllerBase
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

    /// <summary>All Active Knowledge with Confidence + provenance (AC1).</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<KnowledgeListItem>>> List(CancellationToken cancellationToken)
        => Ok(await knowledge.ListAsync(cancellationToken));

    /// <summary>Review-status Knowledge (T8: hygiene/rate moved items here).</summary>
    [HttpGet("review")]
    public async Task<ActionResult<ReviewKnowledgeResult>> Review(CancellationToken cancellationToken)
        => Ok(await knowledge.ListReviewAsync(cancellationToken));

    /// <summary>Archived Knowledge — restore or permanently remove (T8 AC4).</summary>
    [HttpGet("archived")]
    public async Task<ActionResult<IReadOnlyList<KnowledgeListItem>>> Archived(CancellationToken cancellationToken)
        => Ok(await knowledge.ListArchivedAsync(cancellationToken));

    /// <summary>Toggle the per-item private marker (AC2).</summary>
    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> UpdateVisibility(
        Guid id, [FromBody] UpdateKnowledgeVisibilityRequest request, CancellationToken cancellationToken)
    {
        await knowledge.UpdateVisibilityAsync(id, request.IsPrivate, cancellationToken);
        return NoContent();
    }

    /// <summary>Restore an Archived item back to Active (T8 AC4).</summary>
    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid id, CancellationToken cancellationToken)
    {
        await knowledge.RestoreAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>Delete an item; it is then gone from retrieval (AC4).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await knowledge.DeleteAsync(id, cancellationToken);
        return NoContent();
    }

    /// <summary>rate_knowledge: useful → +0.1 (capped), not useful → cleared (AC3).</summary>
    [HttpPost("{id:guid}/rate")]
    public async Task<ActionResult<RateKnowledgeResult>> Rate(
        Guid id, [FromBody] RateKnowledgeRequest request, CancellationToken cancellationToken)
        => Ok(await knowledge.RateAsync(id, request.Useful, cancellationToken));
}
