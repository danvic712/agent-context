using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace AgentContext.Host.Controllers;

/// <summary>
/// Analytics surface (T7, issue #8): the session overview report (spec US27) and
/// the maintained model pricing table (spec US28). Thin adapters over the
/// application seams.
/// </summary>
[ApiController]
[Route("api/analytics")]
public sealed class AnalyticsController(
    IAnalyticsAppService analytics,
    IPricingAppService pricing) : ControllerBase
{
    /// <summary>
    /// The session overview: sessions / tokens / cost, with breakdowns by domain
    /// and agent. Optional filters: workspaceId, domain, agent.
    /// </summary>
    [HttpGet("overview")]
    public async Task<ActionResult<AnalyticsOverview>> Overview(
        [FromQuery] Guid? workspaceId = null,
        [FromQuery] string? domain = null,
        [FromQuery] string? agent = null,
        CancellationToken cancellationToken = default)
        => Ok(await analytics.GetOverviewAsync(workspaceId, domain, agent, cancellationToken));

    /// <summary>All model pricing rows (ordered by model name).</summary>
    [HttpGet("pricing")]
    public async Task<ActionResult<IReadOnlyList<ModelPricingDto>>> ListPricing(CancellationToken cancellationToken)
        => Ok(await pricing.ListAsync(cancellationToken));

    /// <summary>Create or update a model's per-token pricing (upsert by model name).</summary>
    [HttpPut("pricing")]
    public async Task<ActionResult<ModelPricingDto>> SavePricing(
        [FromBody] SaveModelPricingRequest request, CancellationToken cancellationToken)
        => Ok(await pricing.SaveAsync(request, cancellationToken));

    /// <summary>Delete a model's pricing row; no-op when the model is unknown.</summary>
    [HttpDelete("pricing/{model}")]
    public async Task<IActionResult> DeletePricing(string model, CancellationToken cancellationToken)
    {
        await pricing.DeleteAsync(model, cancellationToken);
        return NoContent();
    }
}
