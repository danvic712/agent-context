namespace AgentContext.Application.Contracts;

/// <summary>
/// Reads strings from the single JSON localization store (ADR 0008:
/// <c>i18n/{locale}.json</c>, embedded at build time). Backend error messages and
/// the extraction prompt resolve through this service in the configured language;
/// a missing key falls back to en-US, then to the key itself.
/// </summary>
public interface ITranslationService
{
    /// <summary>
    /// Returns the localized string for <paramref name="key"/> (a full dotted key,
    /// e.g. <c>errors.llm.baseUrlRequired</c> or <c>prompts.extraction</c>) in the
    /// requested locale. Falls back to en-US, then to the key. <paramref name="args"/>
    /// are interpolated as <c>{0}</c>/<c>{1}</c> positional placeholders.
    /// </summary>
    string Get(string key, string locale, params object?[] args);

    /// <summary>
    /// Returns the localized message for a <see cref="AgentContext.Application.Localization.LocalizedException"/>
    /// error code (prefixed with <c>errors.</c>) in the requested locale.
    /// </summary>
    string GetError(string errorCode, string locale, params object?[] args);

    /// <summary>Normalizes a requested locale to a supported one (falls back to en-US).</summary>
    string ResolveLocale(string? locale);
}
