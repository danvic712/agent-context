using AgentContext.Application.Localization;

namespace AgentContext.Application.Tests.Localization;

public sealed class LocalesAppServiceTests
{
    [Fact]
    public void LoadsKeysAcrossGroupedLocaleResources()
    {
        var locales = new LocalesAppService();

        Assert.Equal("Loading…", locales.Get("common.loading", "en-US"));
        Assert.Equal("知识库", locales.Get("knowledge.libraryTitle", "zh-CN"));
        Assert.Equal("Platform settings", locales.Get("settings.pageTitle", "en-US"));
        Assert.Equal(
            "This language isn't available yet. Choose English or Simplified Chinese.",
            locales.GetError("settings.unsupportedLanguage", "en-US", "fr-FR"));
        Assert.Equal(
            "连接已准备好。",
            locales.Get("inference.verification.success", "zh-CN"));
        Assert.StartsWith(
            "You are the Learning Engine",
            locales.Get("prompts.extraction", "en-US"));
    }

    [Fact]
    public void MissingLocaleFallsBackToEnglishAcrossGroupedResources()
    {
        var locales = new LocalesAppService();

        Assert.Equal("Knowledge library", locales.Get("knowledge.libraryTitle", "fr-FR"));
    }
}
