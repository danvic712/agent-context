using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace AgentContext.Host.Controllers;

/// <summary>
/// Knowledge hygiene trigger (T8, issue #9 / spec US20): runs one decay pass on
/// demand (the background worker runs it on a timer too; this surface lets the UI
/// trigger it and lets tests drive the same seam over REST).
/// </summary>
[ApiController]
[Route("api/knowledge/hygiene")]
public sealed class KnowledgeHygieneController(IKnowledgeHygieneAppService hygiene) : ControllerBase
{
    /// <summary>Runs one hygiene pass; returns what it changed (AC1/AC2).</summary>
    [HttpPost("run")]
    public async Task<ActionResult<HygieneResult>> Run(CancellationToken cancellationToken)
        => Ok(await hygiene.RunOnceAsync(cancellationToken));
}
