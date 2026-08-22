using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Application.Enums;
using AgentContext.Application.Learning;
using AgentContext.Application.Tests.TestSupport;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using DomainEntity = AgentContext.Domain.Entities.Domain;
using KnowledgeEntity = AgentContext.Domain.Entities.Knowledge;

namespace AgentContext.Application.Tests.Learning;

public sealed class LearningPipelineAppServiceTests
{
    [Fact]
    public async Task Process_records_chat_and_embedding_usage_separately_from_reported_usage()
    {
        var sessionId = Guid.NewGuid();
        var chatRouteId = Guid.NewGuid();
        var embeddingRouteId = Guid.NewGuid();
        var reported = new Usage
        {
            SessionId = sessionId,
            Model = "external-model",
            InputTokens = 500,
            OutputTokens = 100,
            Source = UsageSource.ReportedSession,
        };
        var rows = new List<Usage> { reported };
        var context = CreateContext(sessionId, rows);
        var llm = new Mock<ILlmClient>();
        llm
            .Setup(item => item.ExtractKnowledgeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCallResult<IReadOnlyList<KnowledgeExtraction>>(
                [new(KnowledgeType.Solution, "Reusable solution", "A useful platform solution", 0.8)],
                new LlmUsage(100, 25, 40),
                chatRouteId,
                "chat-model-snapshot"));
        llm
            .Setup(item => item.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCallResult<float[]>(
                [1f],
                new LlmUsage(12, 0, 0),
                embeddingRouteId,
                "embedding-model-snapshot"));
        var service = CreateService(context.Object, llm.Object);

        var result = await service.ProcessAsync(sessionId);

        Assert.Equal(PipelineOutcome.Completed, result.Outcome);
        Assert.Equal(3, rows.Count);
        Assert.Single(rows, row => row.Source == UsageSource.ReportedSession);
        Assert.Collection(
            rows.Where(row => row.Source == UsageSource.LearningEngine),
            chat =>
            {
                Assert.Equal(InferenceCapability.Chat, chat.Capability);
                Assert.Equal(chatRouteId, chat.InferenceRouteId);
                Assert.Equal("chat-model-snapshot", chat.Model);
                Assert.Equal(sessionId, chat.SessionId);
            },
            embedding =>
            {
                Assert.Equal(InferenceCapability.Embedding, embedding.Capability);
                Assert.Equal(embeddingRouteId, embedding.InferenceRouteId);
                Assert.Equal("embedding-model-snapshot", embedding.Model);
                Assert.Equal(sessionId, embedding.SessionId);
            });
    }

    [Fact]
    public async Task Missing_chat_usage_does_not_prevent_the_pipeline_from_completing()
    {
        var sessionId = Guid.NewGuid();
        var rows = new List<Usage>();
        var context = CreateContext(sessionId, rows);
        var llm = new Mock<ILlmClient>();
        llm
            .Setup(item => item.ExtractKnowledgeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LlmCallResult<IReadOnlyList<KnowledgeExtraction>>([], null, Guid.NewGuid(), "chat-model"));
        var service = CreateService(context.Object, llm.Object);

        var result = await service.ProcessAsync(sessionId);

        Assert.Equal(PipelineOutcome.Completed, result.Outcome);
        Assert.Empty(rows);
        llm.Verify(item => item.EmbedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    private static LearningPipelineAppService CreateService(
        AgentContextDbContext context,
        ILlmClient llm)
    {
        var inference = new Mock<IInferenceConfigurationAppService>();
        inference
            .Setup(item => item.GetRuntimeOptionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InferenceRuntimeOptions(
                new(Guid.NewGuid(), "https://provider.test/v1", "key", "chat-model"),
                new(Guid.NewGuid(), "https://provider.test/v1", "key", "embedding-model")));
        var recorder = new LearningUsageRecorder(
            context,
            Mock.Of<ILogger<LearningUsageRecorder>>());
        return new LearningPipelineAppService(
            context,
            llm,
            Mock.Of<ILogger<LearningPipelineAppService>>(),
            inference.Object,
            recorder);
    }

    private static Mock<AgentContextDbContext> CreateContext(Guid sessionId, ICollection<Usage> usageRows)
    {
        var session = new Session
        {
            Id = sessionId,
            WorkspaceId = Guid.NewGuid(),
            DomainId = Guid.NewGuid(),
            AgentName = "test-agent",
            Task = "test task",
            Conclusion = "test conclusion",
            SummaryJson = "{}",
            Status = SessionStatus.Pending,
            Domain = new DomainEntity { Id = Guid.NewGuid(), WorkspaceId = Guid.NewGuid(), Name = "test" },
        };
        session.DomainId = session.Domain!.Id;

        var options = new DbContextOptionsBuilder<AgentContextDbContext>().Options;
        var context = new Mock<AgentContextDbContext>(MockBehavior.Strict, options);
        context.SetupGet(db => db.Sessions).Returns(MockDbSetFactory.Create([session]));
        context.SetupGet(db => db.Knowledge).Returns(MockDbSetFactory.Create<KnowledgeEntity>([]));
        var usage = new Mock<DbSet<Usage>>();
        usage.Setup(set => set.Add(It.IsAny<Usage>())).Callback<Usage>(usageRows.Add);
        context.SetupGet(db => db.Usage).Returns(usage.Object);
        context
            .Setup(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        return context;
    }
}
