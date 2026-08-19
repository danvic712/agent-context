using AgentContext.Application.Learning;

namespace AgentContext.Application.Contracts;

/// <summary>
/// Platform preferences stored in the database. Language and theme are the
/// public Settings surface. The legacy LlmOptions methods remain as a
/// compatibility seam for older application-service callers and tests; the
/// production Learning Engine resolves the dedicated inference tables.
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

    /// <summary>
    /// The platform theme (T12): <c>light</c> / <c>dark</c> / <c>system</c> from the
    /// settings table, falling back to <c>system</c> when missing or invalid.
    /// </summary>
    Task<string> GetThemeAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Validates and persists the platform theme. Unsupported value →
    /// <see cref="Localization.LocalizedException"/> (400, <c>settings.unsupportedTheme</c>).
    /// </summary>
    Task SaveThemeAsync(string theme, CancellationToken cancellationToken = default);
}
