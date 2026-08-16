using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Application.Learning;
using Microsoft.AspNetCore.Mvc;

namespace AgentContext.Host.Controllers;

/// <summary>
/// Platform settings surface (T10, issue #11): reads and edits the Learning
/// Engine's LLM endpoint (ADR 0003), stored in the DB-backed settings table —
/// setter-uppable at runtime, no restart needed (LlmClient resolves per call).
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
        try
        {
            await settings.SaveLlmOptionsAsync(request, cancellationToken);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var saved = await settings.GetLlmOptionsAsync(cancellationToken);
        return Ok(new LlmOptionsDto(
            saved is not null,
            saved?.BaseUrl,
            saved is null ? null : Mask(saved.ApiKey),
            saved?.Model,
            saved?.EmbeddingModel));
    }

    /// <summary>Masks an API key, keeping only a short prefix (e.g. "sk-…").</summary>
    internal static string Mask(string apiKey)
        => apiKey.Length <= 6
            ? "••••"
            : apiKey[..6] + "••••••";
}
