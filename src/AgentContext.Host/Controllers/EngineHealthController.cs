using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace AgentContext.Host.Controllers;

/// <summary>
/// Learning Engine health view (T8, issue #9 / spec US29): queue depth, failures
/// and retries, read from the Postgres-as-queue sessions table.
/// </summary>
[ApiController]
[Route("api/health/engine")]
public sealed class EngineHealthController(IEngineHealthAppService engine) : ControllerBase
{
    /// <summary>Pending/processing/failed/retry-scheduled session counts (AC3).</summary>
    [HttpGet]
    public async Task<ActionResult<EngineHealthDto>> Get(CancellationToken cancellationToken)
        => Ok(await engine.GetHealthAsync(cancellationToken));
}
