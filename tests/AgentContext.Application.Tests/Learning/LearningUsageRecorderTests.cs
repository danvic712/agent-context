using AgentContext.Application.Dtos;
using AgentContext.Application.Learning;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace AgentContext.Application.Tests.Learning;

public sealed class LearningUsageRecorderTests
{
    [Theory]
    [InlineData(InferenceCapability.Chat)]
    [InlineData(InferenceCapability.Embedding)]
    public async Task Record_persists_learning_engine_usage_with_route_model_and_session(
        InferenceCapability capability)
    {
        var sessionId = Guid.NewGuid();
        var routeId = Guid.NewGuid();
        var rows = new List<Usage>
        {
            new()
            {
                SessionId = sessionId,
                Model = "external-model",
                Source = UsageSource.ReportedSession,
            },
        };
        var context = CreateContext(rows);
        var recorder = new LearningUsageRecorder(context.Object, Mock.Of<ILogger<LearningUsageRecorder>>());

        await recorder.RecordAsync(
            sessionId,
            capability,
            new LlmCallResult<float[]>(
                [],
                new LlmUsage(100, 25, 40),
                routeId,
                "platform-model-snapshot"));

        Assert.Equal(2, rows.Count);
        var platform = Assert.Single(rows, row => row.Source == UsageSource.LearningEngine);
        Assert.Equal(sessionId, platform.SessionId);
        Assert.Equal(routeId, platform.InferenceRouteId);
        Assert.Equal("platform-model-snapshot", platform.Model);
        Assert.Equal(capability, platform.Capability);
        Assert.Equal(100, platform.InputTokens);
        Assert.Equal(25, platform.CachedInputTokens);
        Assert.Equal(40, platform.OutputTokens);
        Assert.Single(rows, row => row.Source == UsageSource.ReportedSession);
        context.Verify(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Missing_usage_is_observable_without_creating_a_zero_count_row()
    {
        var rows = new List<Usage>
        {
            new()
            {
                SessionId = Guid.NewGuid(),
                Model = "external-model",
                Source = UsageSource.ReportedSession,
            },
        };
        var context = CreateContext(rows);
        var recorder = new LearningUsageRecorder(context.Object, Mock.Of<ILogger<LearningUsageRecorder>>());

        await recorder.RecordAsync(
            Guid.NewGuid(),
            InferenceCapability.Chat,
            new LlmCallResult<float[]>([], null, Guid.NewGuid(), "platform-model-snapshot"));

        Assert.Single(rows);
        Assert.DoesNotContain(rows, row => row.Source == UsageSource.LearningEngine);
        context.Verify(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Sessionless_platform_usage_keeps_the_optional_session_binding_null()
    {
        var rows = new List<Usage>();
        var context = CreateContext(rows);
        var recorder = new LearningUsageRecorder(context.Object, Mock.Of<ILogger<LearningUsageRecorder>>());

        await recorder.RecordAsync(
            null,
            InferenceCapability.Embedding,
            new LlmCallResult<float[]>(
                [],
                new LlmUsage(12, 0, 0),
                Guid.NewGuid(),
                "embedding-model-snapshot"));

        var platform = Assert.Single(rows);
        Assert.Equal(UsageSource.LearningEngine, platform.Source);
        Assert.Null(platform.SessionId);
        Assert.Equal(InferenceCapability.Embedding, platform.Capability);
    }

    private static Mock<AgentContextDbContext> CreateContext(ICollection<Usage> rows)
    {
        var options = new DbContextOptionsBuilder<AgentContextDbContext>().Options;
        var context = new Mock<AgentContextDbContext>(MockBehavior.Strict, options);
        var usage = new Mock<DbSet<Usage>>();
        usage
            .Setup(set => set.Add(It.IsAny<Usage>()))
            .Callback<Usage>(rows.Add);
        context.SetupGet(db => db.Usage).Returns(usage.Object);
        context
            .Setup(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        return context;
    }
}
