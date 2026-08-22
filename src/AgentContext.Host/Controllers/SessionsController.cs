using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace AgentContext.Host.Controllers;

/// <summary>Session recording + overview endpoints (T2).</summary>
[ApiController]
[Route("api/sessions")]
public sealed class SessionsController(ISaveSessionAppService sessions) : ControllerBase
{
    /// <summary>Record a session (save_session over REST): persists Session + Usage with status pending.</summary>
    [HttpPost]
    public async Task<ActionResult<SaveSessionResult>> Save([FromBody] SaveSessionRequest request, CancellationToken cancellationToken)
    {
        var result = await sessions.SaveAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = result.SessionId }, result);
    }

    /// <summary>Session detail including Usage rows (spec §6.2 overview data).</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SessionDetail>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await sessions.GetAsync(id, cancellationToken));

    /// <summary>All sessions with token rollups from Usage.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SessionListItem>>> List(CancellationToken cancellationToken)
        => Ok(await sessions.ListAsync(cancellationToken));
}
