using System.Text.Json;
using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Host.Controllers;
using Moq;

namespace AgentContext.Host.Tests;

public sealed class SessionsControllerTests
{
    [Fact]
    public async Task Save_forwards_the_nested_usage_payload_from_the_rest_contract()
    {
        SaveSessionRequest? captured = null;
        var sessions = new Mock<ISaveSessionAppService>(MockBehavior.Strict);
        sessions.Setup(service => service.SaveAsync(
                It.IsAny<SaveSessionRequest>(),
                It.IsAny<CancellationToken>()))
            .Callback<SaveSessionRequest, CancellationToken>((request, _) => captured = request)
            .ReturnsAsync(new SaveSessionResult(Guid.NewGuid(), "dev", false));

        var request = JsonSerializer.Deserialize<SaveSessionRequest>(
            """
            {
              "domain": "dev",
              "task": "task",
              "conclusion": "conclusion",
              "usage": {
                "model": "provider/model-snapshot",
                "inputTokens": 100,
                "cachedInputTokens": 25,
                "outputTokens": 40
              }
            }
            """,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));
        Assert.NotNull(request);

        var controller = new SessionsController(sessions.Object);
        await controller.Save(request!, CancellationToken.None);

        Assert.NotNull(captured);
        Assert.Equal("provider/model-snapshot", captured!.Usage!.Model);
        Assert.Equal(100, captured.Usage.InputTokens);
        Assert.Equal(25, captured.Usage.CachedInputTokens);
        Assert.Equal(40, captured.Usage.OutputTokens);
    }
}
