using AgentContext.Application.Contracts;
using AgentContext.Application.Localization;

namespace AgentContext.Tests.SeamTests;

/// <summary>
/// The shared JSON localization store (T11 / ADR 0008): TranslationService reads
/// the embedded i18n resources, resolves dotted keys, and falls back
/// en-US → raw key. These are pure unit tests — no database involved.
/// </summary>
public sealed class TranslationServiceTests
{
    private readonly ITranslationService _translations = new TranslationService();

    [Fact]
    public void Resolves_existing_key_in_the_requested_locale()
    {
        var zh = _translations.Get("errors.setup.passwordTooShort", "zh-CN");
        Assert.Contains("8", zh);

        var en = _translations.Get("errors.setup.passwordTooShort", "en-US");
        Assert.Contains("8 characters", en);

        Assert.NotEqual(zh, en);
    }

    [Fact]
    public void Missing_key_falls_back_to_english_then_to_the_key_itself()
    {
        // The key exists in zh-CN but not en-US → falls back to en-US.
        var fromZh = _translations.Get("ui.wizard.chinese", "zh-CN");
        Assert.Equal("中文 (简体)", fromZh);

        // Unknown key in any locale → the key itself.
        var unknown = _translations.Get("errors.nope.missing", "zh-CN");
        Assert.Equal("errors.nope.missing", unknown);
    }

    [Fact]
    public void Missing_locale_falls_back_to_english()
    {
        var message = _translations.Get("errors.llm.apiKeyRequired", "fr-FR");
        Assert.Contains("ApiKey", message);
    }

    [Fact]
    public void GetError_interpolates_positional_arguments()
    {
        var en = _translations.GetError(ErrorCodes.Skill.SlugExists, "en-US", "coding-guide", "dev");
        Assert.Contains("\"coding-guide\"", en);
        Assert.Contains("\"dev\"", en);

        var zh = _translations.GetError(ErrorCodes.Skill.SlugExists, "zh-CN", "coding-guide", "dev");
        Assert.Contains("coding-guide", zh);
        Assert.Contains("dev", zh);
    }

    [Fact]
    public void ResolveLocale_normalizes_case_and_falls_back()
    {
        Assert.Equal("en-US", _translations.ResolveLocale("en-us"));
        Assert.Equal("zh-CN", _translations.ResolveLocale("ZH-CN"));
        Assert.Equal("en-US", _translations.ResolveLocale("fr-FR"));
        Assert.Equal("en-US", _translations.ResolveLocale(null));
    }

    [Fact]
    public void All_error_codes_resolve_in_both_locales()
    {
        // Every code in ErrorCodes must have a translation in both locales —
        // otherwise the backend falls back silently and messages drift.
        var codes = new[]
        {
            ErrorCodes.Setup.DisplayNameRequired,
            ErrorCodes.Setup.EmailInvalid,
            ErrorCodes.Setup.PasswordTooShort,
            ErrorCodes.Setup.AlreadyConfigured,
            ErrorCodes.Llm.BaseUrlRequired,
            ErrorCodes.Llm.BaseUrlInvalid,
            ErrorCodes.Llm.ApiKeyRequired,
            ErrorCodes.Llm.ModelRequired,
            ErrorCodes.Llm.NotConfigured,
            ErrorCodes.Skill.SlugExists,
            ErrorCodes.Skill.SlugInvalid,
            ErrorCodes.Skill.NameRequired,
            ErrorCodes.Skill.NotFound,
            ErrorCodes.Skill.SlugNotFound,
            ErrorCodes.Pricing.ModelRequired,
            ErrorCodes.Pricing.NegativeCost,
            ErrorCodes.Session.FullContextRequired,
            ErrorCodes.Session.NotFound,
            ErrorCodes.Knowledge.NotFound,
            ErrorCodes.Knowledge.NotArchived,
            ErrorCodes.Platform.NotConfigured,
            ErrorCodes.Settings.UnsupportedLanguage,
            ErrorCodes.InternalError,
        };

        foreach (var code in codes)
        {
            var en = _translations.GetError(code, "en-US");
            Assert.NotEqual($"errors.{code}", en);
            var zh = _translations.GetError(code, "zh-CN");
            Assert.NotEqual($"errors.{code}", zh);
        }
    }
}
