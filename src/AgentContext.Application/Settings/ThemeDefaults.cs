namespace AgentContext.Application.Settings;

/// <summary>
/// The platform theme vocabulary (T12): <c>light</c> / <c>dark</c> / <c>system</c>,
/// stored in <c>settings.theme</c> and resolved per call (same seam as the language).
/// Missing/invalid values fall back to <see cref="DefaultTheme"/> (<c>system</c> —
/// the UI follows the OS preference).
/// </summary>
public static class ThemeDefaults
{
    public const string Light = "light";
    public const string Dark = "dark";
    public const string System = "system";

    public const string DefaultTheme = System;

    public static readonly IReadOnlyList<string> SupportedThemes = [Light, Dark, System];

    /// <summary>Normalizes a requested theme; returns <c>true</c> when it was supported.</summary>
    public static bool TryNormalize(string? theme, out string normalized)
    {
        if (theme is not null)
        {
            foreach (var supported in SupportedThemes)
            {
                if (string.Equals(supported, theme, StringComparison.OrdinalIgnoreCase))
                {
                    normalized = supported;
                    return true;
                }
            }
        }

        normalized = DefaultTheme;
        return false;
    }

    /// <summary>Normalizes a requested theme, always returning a supported one.</summary>
    public static string Normalize(string? theme)
    {
        TryNormalize(theme, out var normalized);
        return normalized;
    }
}
