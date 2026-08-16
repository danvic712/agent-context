namespace AgentContext.Application.Localization;

/// <summary>
/// Platform locale vocabulary (ADR 0008): the supported BCP-47 locales and the
/// normalization rules. The platform language lives in the <c>settings</c> table
/// (<c>settings.language</c>) and is resolved per call — missing/invalid values
/// fall back to <see cref="DefaultLocale"/>.
/// </summary>
public static class LocalizationDefaults
{
    public const string DefaultLocale = "en-US";

    /// <summary>Locales with a resource file in <c>i18n/{locale}.json</c>.</summary>
    public static readonly IReadOnlyList<string> SupportedLocales = ["en-US", "zh-CN"];

    /// <summary>
    /// Normalizes a requested locale to its canonical form. Case-insensitive
    /// match (<c>en-us</c> → <c>en-US</c>); unknown or missing → <see cref="DefaultLocale"/>.
    /// Returns <c>true</c> when the input was a supported locale.
    /// </summary>
    public static bool TryNormalize(string? locale, out string normalized)
    {
        if (locale is not null)
        {
            foreach (var supported in SupportedLocales)
            {
                if (string.Equals(supported, locale, StringComparison.OrdinalIgnoreCase))
                {
                    normalized = supported;
                    return true;
                }
            }
        }

        normalized = DefaultLocale;
        return false;
    }

    /// <summary>Normalizes a requested locale, always returning a supported one.</summary>
    public static string Normalize(string? locale)
    {
        TryNormalize(locale, out var normalized);
        return normalized;
    }
}
