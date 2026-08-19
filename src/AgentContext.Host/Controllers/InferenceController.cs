using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace AgentContext.Host.Controllers;

/// <summary>
/// Platform inference configuration. This surface intentionally lives outside
/// Settings because it owns provider connections, route bindings, and secrets.
/// </summary>
[ApiController]
[Route("api/inference")]
public sealed class InferenceController(IInferenceConfigurationAppService inference) : ControllerBase
{
    [HttpGet("configuration")]
    public async Task<ActionResult<InferenceConfigurationDto>> GetConfiguration(CancellationToken cancellationToken)
        => Ok(await inference.GetAsync(cancellationToken));

    [HttpPut("configuration")]
    public async Task<ActionResult<InferenceConfigurationDto>> SaveConfiguration(
        [FromBody] InferenceConfigurationInput request,
        CancellationToken cancellationToken)
        => Ok(await inference.SaveAsync(request, cancellationToken));

    [HttpPost("configuration/verify")]
    public async Task<ActionResult<InferenceValidationResult>> VerifyConfiguration(
        [FromBody] InferenceConfigurationInput request,
        CancellationToken cancellationToken)
        => Ok(await inference.VerifyAsync(request, cancellationToken));
}
