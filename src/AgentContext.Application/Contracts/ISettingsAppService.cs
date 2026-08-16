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

    /// <summary>
    /// The platform language (T11): a supported BCP-47 locale from the settings
    /// table, falling back to <c>en-US</c> when missing or invalid. Resolved per
    /// call so a change applies without a restart.
    /// </summary>
    Task<string> GetLanguageAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and persists the platform language. Unsupported locale →
    /// <see cref="Localization.LocalizedException"/> (400, <c>settings.unsupportedLanguage</c>);
    /// the canonical form (e.g. <c>en-us</c> → <c>en-US</c>) is stored.
    /// </summary>
    Task SaveLanguageAsync(string locale, CancellationToken cancellationToken = default);
}
