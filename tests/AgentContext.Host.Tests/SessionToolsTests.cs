using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Host.Mcp;
using Moq;

namespace AgentContext.Host.Tests;

public sealed class SessionToolsTests
{
    [Fact]
    public async Task Save_session_forwards_the_same_usage_payload_as_rest()
    {
        SaveSessionRequest? captured = null;
        var sessions = new Mock<ISaveSessionAppService>(MockBehavior.Strict);
        sessions.Setup(service => service.SaveAsync(
                It.IsAny<SaveSessionRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<SaveSessionRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new SaveSessionResult(Guid.NewGuid(), "dev", false));

        var tools = new SessionTools(
            sessions.Object,
            new Mock<ISettingsAppService>(MockBehavior.Strict).Object,
            new Mock<ITranslationService>(MockBehavior.Strict).Object);

        await tools.SaveSession(
            domain: "dev",
            task: "task",
            conclusion: "conclusion",
            usage: new SessionUsageInput("provider/model-snapshot", 100, 25, 40),
            cancellationToken: CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("provider/model-snapshot", captured!.Usage!.Model);
        Assert.Equal(100, captured.Usage.InputTokens);
        Assert.Equal(25, captured.Usage.CachedInputTokens);
        Assert.Equal(40, captured.Usage.OutputTokens);
    }
}
