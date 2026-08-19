using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace AgentContext.Host.Controllers;

/// <summary>
/// Platform preferences surface: language and theme remain settings concerns.
/// Inference providers and routes live under <c>/api/inference</c>.
/// </summary>
[ApiController]
[Route("api/settings")]
public sealed class SettingsController(ISettingsAppService settings) : ControllerBase
{
    /// <summary>
    /// The platform language (T11): a supported BCP-47 locale, defaulting to
    /// en-US when nothing is stored yet. Invalid stored values also fall back.
    /// </summary>
    [HttpGet("language")]
    public async Task<ActionResult<SettingsLanguageDto>> GetLanguage(CancellationToken cancellationToken)
        => Ok(new SettingsLanguageDto(await settings.GetLanguageAsync(cancellationToken)));

    /// <summary>
    /// Validates and persists the platform language (T11 AC1). Unsupported
    /// locale → 400 <c>{ errorCode, message }</c> via the global filter.
    /// </summary>
    [HttpPut("language")]
    public async Task<ActionResult<SettingsLanguageDto>> SaveLanguage(
        [FromBody] SettingsLanguageDto request, CancellationToken cancellationToken)
    {
        await settings.SaveLanguageAsync(request.Language, cancellationToken);
        return Ok(new SettingsLanguageDto(await settings.GetLanguageAsync(cancellationToken)));
    }

    /// <summary>
    /// The platform theme (T12): light / dark / system, defaulting to system
    /// (follow the OS) when nothing is stored yet.
    /// </summary>
    [HttpGet("theme")]
    public async Task<ActionResult<SettingsThemeDto>> GetTheme(CancellationToken cancellationToken)
        => Ok(new SettingsThemeDto(await settings.GetThemeAsync(cancellationToken)));

    /// <summary>
    /// Validates and persists the platform theme (T12). Unsupported value → 400
    /// <c>{ errorCode, message }</c> via the global filter.
    /// </summary>
    [HttpPut("theme")]
    public async Task<ActionResult<SettingsThemeDto>> SaveTheme(
        [FromBody] SettingsThemeDto request, CancellationToken cancellationToken)
    {
        await settings.SaveThemeAsync(request.Theme, cancellationToken);
        return Ok(new SettingsThemeDto(await settings.GetThemeAsync(cancellationToken)));
    }

}
