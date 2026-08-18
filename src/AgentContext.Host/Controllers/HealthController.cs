using AgentContext.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgentContext.Host.Controllers;

/// <summary>Liveness/readiness probe used by the UI and Docker health checks.</summary>
[ApiController]
[Route("api/health")]
public sealed class HealthController(AgentContextDbContext db, IConfiguration configuration) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult> Get(CancellationToken cancellationToken)
    {
        try
        {
            _ = await db.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);
            return Ok(new { status = "ok", database = "ok" });
        }
        catch (Exception ex)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "degraded", database = "unavailable", error = ex.Message });
        }
    }

    /// <summary>
    /// The observability dashboard URL for the UI's "open dashboard" entry — the
    /// portal doesn't know it (it only has the OTLP endpoint), so the deploy
    /// surface injects it: Compose sets the same-origin
    /// <c>DASHBOARD_URL=http://localhost:8080/monitor/resources</c>, while AppHost mode
    /// injects the in-process dashboard surface.
    /// </summary>
    [HttpGet("dashboard")]
    public ActionResult GetDashboardUrl() => Ok(new { url = configuration["DASHBOARD_URL"] });
}
