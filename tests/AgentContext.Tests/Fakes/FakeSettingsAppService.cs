using AgentContext.Application.Contracts;
using AgentContext.Application.Learning;

namespace AgentContext.Tests.Fakes;

/// <summary>Deterministic in-memory <see cref="ISettingsAppService"/> for client tests.</summary>
public sealed class FakeSettingsAppService : ISettingsAppService
{
    private LlmOptions? _options;

    public FakeSettingsAppService(LlmOptions? options) => _options = options;

    public Task<LlmOptions?> GetLlmOptionsAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(_options);

    public Task SaveLlmOptionsAsync(LlmOptions options, CancellationToken cancellationToken = default)
    {
        _options = options;
        return Task.CompletedTask;
    }
}
