using System.Net;
using AgentContext.Application.Contracts;
using AgentContext.Application.Localization;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AgentContext.Application.Settings;

/// <inheritdoc cref="ISettingsAppService"/>
/// <summary>
/// Reads and writes platform preferences in the <c>settings</c> key/value table.
/// The platform language (T11) is a single key resolved per call.
/// </summary>
public sealed class SettingsAppService(AgentContextDbContext db) : ISettingsAppService
{
    public async Task<string> GetLanguageAsync(CancellationToken cancellationToken = default)
    {
        var stored = await db.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Key == SettingKeys.Language, cancellationToken);

        return LocalizationDefaults.Normalize(stored?.Value);
    }

    public async Task SaveLanguageAsync(string locale, CancellationToken cancellationToken = default)
    {
        if (!LocalizationDefaults.TryNormalize(locale, out var normalized))
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Settings.UnsupportedLanguage, locale ?? string.Empty);
        }

        var existing = await db.AppSettings
            .FirstOrDefaultAsync(e => e.Key == SettingKeys.Language, cancellationToken);
        if (existing is null)
        {
            db.AppSettings.Add(new AppSetting { Key = SettingKeys.Language, Value = normalized });
        }
        else
        {
            existing.Value = normalized;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<string> GetThemeAsync(CancellationToken cancellationToken = default)
    {
        var stored = await db.AppSettings.AsNoTracking()
            .FirstOrDefaultAsync(e => e.Key == SettingKeys.Theme, cancellationToken);

        return ThemeDefaults.Normalize(stored?.Value);
    }

    public async Task SaveThemeAsync(string theme, CancellationToken cancellationToken = default)
    {
        if (!ThemeDefaults.TryNormalize(theme, out var normalized))
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Settings.UnsupportedTheme, theme ?? string.Empty);
        }

        var existing = await db.AppSettings
            .FirstOrDefaultAsync(e => e.Key == SettingKeys.Theme, cancellationToken);
        if (existing is null)
        {
            db.AppSettings.Add(new AppSetting { Key = SettingKeys.Theme, Value = normalized });
        }
        else
        {
            existing.Value = normalized;
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
