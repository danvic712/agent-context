using AgentContext.Application.Hygiene;
using AgentContext.Application.Tests.TestSupport;
using AgentContext.Domain;
using KnowledgeEntity = AgentContext.Domain.Entities.Knowledge;

namespace AgentContext.Application.Tests.Knowledge;

public sealed class KnowledgeHygieneAppServiceTests
{
    [Fact]
    public async Task RunOnce_applies_stale_decay_once_and_checkpoints_it()
    {
        var staleAt = DateTimeOffset.UtcNow.AddDays(-65);
        var item = new KnowledgeEntity
        {
            Id = Guid.NewGuid(),
            WorkspaceId = Guid.NewGuid(),
            DomainId = Guid.NewGuid(),
            Title = "stale",
            Content = "stale content",
            Confidence = 0.9,
            Status = KnowledgeStatus.Active,
            CreatedAtUtc = staleAt,
            UpdatedAtUtc = staleAt,
            LastUsedAtUtc = staleAt,
            LastConfidenceDecayAtUtc = staleAt,
        };
        var context = MockKnowledgeDbContext.Create([item]);
        var service = new KnowledgeHygieneAppService(context.Object);

        var first = await service.RunOnceAsync();
        var confidenceAfterFirstRun = item.Confidence;
        var decayCheckpointAfterFirstRun = item.LastConfidenceDecayAtUtc;
        var second = await service.RunOnceAsync();

        Assert.Equal(1, first.Decayed);
        Assert.Equal(0, first.MovedToReview);
        Assert.Equal(0, second.Decayed);
        Assert.Equal(confidenceAfterFirstRun, item.Confidence);
        Assert.True(decayCheckpointAfterFirstRun > staleAt);
        Assert.Equal(decayCheckpointAfterFirstRun, item.LastConfidenceDecayAtUtc);
    }
}
