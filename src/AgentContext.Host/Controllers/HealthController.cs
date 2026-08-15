using AgentContext.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AgentContext.Host.Controllers;

/// <summary>Liveness/readiness probe used by the UI and Docker health checks.</summary>
[ApiController]
[Route("api/health")]
public sealed class HealthController(AgentContextDbContext db) : ControllerBase
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
}
