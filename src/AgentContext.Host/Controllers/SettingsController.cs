using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Application.Learning;
using Microsoft.AspNetCore.Mvc;

namespace AgentContext.Host.Controllers;

/// <summary>
/// Platform settings surface: the Learning Engine's LLM endpoint (T10, ADR 0003)
/// and the platform language (T11, ADR 0008) — both stored in the DB-backed
/// settings table and resolved per call, so no restart is needed.
/// </summary>
[ApiController]
[Route("api/settings")]
public sealed class SettingsController(ISettingsAppService settings) : ControllerBase
{
    /// <summary>
    /// The current LLM endpoint configuration. The API key is never returned in
    /// full — only a masked preview (AC2).
    /// </summary>
    [HttpGet("llm-options")]
    public async Task<ActionResult<LlmOptionsDto>> GetLlmOptions(CancellationToken cancellationToken)
    {
        var options = await settings.GetLlmOptionsAsync(cancellationToken);
        if (options is null)
        {
            return Ok(new LlmOptionsDto(false, null, null, null, null));
        }

        return Ok(new LlmOptionsDto(
            true,
            options.BaseUrl,
            Mask(options.ApiKey),
            options.Model,
            options.EmbeddingModel));
    }

    /// <summary>
    /// Validates and persists the LLM endpoint configuration (AC3). Invalid input
    /// → 400 with the validation message. Applies immediately — the Learning
    /// Engine resolves settings per call (AC4).
    /// </summary>
    [HttpPut("llm-options")]
    public async Task<ActionResult<LlmOptionsDto>> SaveLlmOptions(
        [FromBody] LlmOptions request, CancellationToken cancellationToken)
    {
        await settings.SaveLlmOptionsAsync(request, cancellationToken);

        var saved = await settings.GetLlmOptionsAsync(cancellationToken);
        return Ok(new LlmOptionsDto(
            saved is not null,
            saved?.BaseUrl,
            saved is null ? null : Mask(saved.ApiKey),
            saved?.Model,
            saved?.EmbeddingModel));
    }

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

    /// <summary>Masks an API key, keeping only a short prefix (e.g. "sk-…").</summary>
    internal static string Mask(string apiKey)
        => apiKey.Length <= 6
            ? "••••"
            : apiKey[..6] + "••••••";
}
