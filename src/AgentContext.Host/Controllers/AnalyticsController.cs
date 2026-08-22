using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace AgentContext.Host.Controllers;

/// <summary>
/// Token-only Analytics surface (T7, issue #8). Thin adapter over the
/// application seam.
/// </summary>
[ApiController]
[Route("api/analytics")]
public sealed class AnalyticsController(IAnalyticsAppService analytics) : ControllerBase
{
    /// <summary>
    /// The session overview: sessions and tokens, with breakdowns by domain and
    /// agent. Optional filters: workspaceId, domain, agent.
    /// </summary>
    [HttpGet("overview")]
    public async Task<ActionResult<AnalyticsOverview>> Overview(
        [FromQuery] Guid? workspaceId = null,
        [FromQuery] string? domain = null,
        [FromQuery] string? agent = null,
        CancellationToken cancellationToken = default)
        => Ok(await analytics.GetOverviewAsync(workspaceId, domain, agent, cancellationToken));
}
