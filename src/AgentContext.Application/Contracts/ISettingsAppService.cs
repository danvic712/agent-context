using AgentContext.Application.Learning;

namespace AgentContext.Application.Contracts;

/// <summary>
/// Platform settings, stored in the database (spec: "settings (LLM endpoint)")
/// so the Learning Engine's LLM endpoint is configurable at runtime instead of
/// through app configuration. The settings REST surface is a later ticket; the
/// seam keeps read/write available to setup flows and tests.
/// </summary>
public interface ISettingsAppService
{
    /// <summary>
    /// Returns the stored LLM endpoint configuration when it is present and
    /// valid; <c>null</c> when the endpoint is not configured (or the stored
    /// values are incomplete) — the Learning Engine then stays idle instead of
    /// failing Sessions.
    /// </summary>
    Task<LlmOptions?> GetLlmOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Validates and persists the LLM endpoint configuration (upsert).</summary>
    Task SaveLlmOptionsAsync(LlmOptions options, CancellationToken cancellationToken = default);
}
