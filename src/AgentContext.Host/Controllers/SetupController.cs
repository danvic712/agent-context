using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace AgentContext.Host.Controllers;

/// <summary>
/// First-run wizard endpoints. The UI calls GET /api/setup to decide between the
/// wizard and the app shell, then POST /api/setup once to configure the platform.
/// </summary>
[ApiController]
[Route("api/setup")]
public sealed class SetupController(ISetupAppService setup) : ControllerBase
{
    /// <summary>Whether the platform has been configured (admin User + Workspace exist).</summary>
    [HttpGet]
    public async Task<ActionResult<SetupStatus>> GetStatus(CancellationToken cancellationToken)
        => Ok(await setup.GetStatusAsync(cancellationToken));

    /// <summary>Configure the platform: creates the admin User and a Personal Workspace.</summary>
    [HttpPost]
    public async Task<ActionResult<SetupResult>> Configure([FromBody] SetupRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await setup.ConfigureAsync(request, cancellationToken);
            return Ok(result);
        }
        catch (SetupAlreadyConfiguredException)
        {
            return Conflict(new { message = "The platform has already been configured." });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
