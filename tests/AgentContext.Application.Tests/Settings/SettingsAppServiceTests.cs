using AgentContext.Application.Localization;
using AgentContext.Application.Settings;
using AgentContext.Application.Tests.TestSupport;

namespace AgentContext.Application.Tests.Settings;

public sealed class SettingsAppServiceTests
{
    [Fact]
    public async Task GetLanguage_returns_the_default_when_the_setting_is_missing()
    {
        var service = CreateService();

        var language = await service.GetLanguageAsync();

        Assert.Equal(LocalizationDefaults.DefaultLocale, language);
    }

    [Fact]
    public async Task SaveLanguage_normalizes_and_persists_the_locale()
    {
        var service = CreateService();

        await service.SaveLanguageAsync("en-us");

        Assert.Equal("en-US", await service.GetLanguageAsync());
    }

    [Fact]
    public async Task SaveLanguage_rejects_an_unsupported_locale()
    {
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<LocalizedException>(() => service.SaveLanguageAsync("fr-FR"));

        Assert.Equal(ErrorCodes.Settings.UnsupportedLanguage, exception.ErrorCode);
    }

    [Fact]
    public async Task GetTheme_returns_system_when_the_setting_is_missing()
    {
        var service = CreateService();

        var theme = await service.GetThemeAsync();

        Assert.Equal(ThemeDefaults.System, theme);
    }

    [Fact]
    public async Task SaveTheme_normalizes_and_persists_the_theme()
    {
        var service = CreateService();

        await service.SaveThemeAsync("DARK");

        Assert.Equal(ThemeDefaults.Dark, await service.GetThemeAsync());
    }

    [Fact]
    public async Task SaveTheme_rejects_an_unsupported_theme()
    {
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<LocalizedException>(() => service.SaveThemeAsync("blue"));

        Assert.Equal(ErrorCodes.Settings.UnsupportedTheme, exception.ErrorCode);
    }

    private static SettingsAppService CreateService()
    {
        var context = MockInferenceDbContext.Create();
        return new SettingsAppService(context.Object);
    }
}
