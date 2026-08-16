using AgentContext.Application.Contracts;
using AgentContext.Application.Learning;
using AgentContext.Application.Localization;
using AgentContext.Application.Settings;

namespace AgentContext.Tests.Fakes;

/// <summary>Deterministic in-memory <see cref="ISettingsAppService"/> for client tests.</summary>
public sealed class FakeSettingsAppService : ISettingsAppService
{
    private LlmOptions? _options;
    private string? _language;
    private string? _theme;

    public FakeSettingsAppService(LlmOptions? options, string? language = null, string? theme = null)
    {
        _options = options;
        _language = language;
        _theme = theme;
    }

    public Task<LlmOptions?> GetLlmOptionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_options);

    public Task SaveLlmOptionsAsync(LlmOptions options, CancellationToken cancellationToken = default)
    {
        _options = options;
        return Task.CompletedTask;
    }

    public Task<string> GetLanguageAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(LocalizationDefaults.Normalize(_language));

    public Task SaveLanguageAsync(string locale, CancellationToken cancellationToken = default)
    {
        LocalizationDefaults.TryNormalize(locale, out var normalized);
        _language = normalized;
        return Task.CompletedTask;
    }

    public Task<string> GetThemeAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(ThemeDefaults.Normalize(_theme));

    public Task SaveThemeAsync(string theme, CancellationToken cancellationToken = default)
    {
        ThemeDefaults.TryNormalize(theme, out var normalized);
        _theme = normalized;
        return Task.CompletedTask;
    }
}
