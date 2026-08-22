using System.Net;
using AgentContext.Application.Dtos;
using AgentContext.Application.Localization;
using AgentContext.Application.KnowledgeManagement;
using AgentContext.Application.Tests.TestSupport;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using DomainEntity = AgentContext.Domain.Entities.Domain;
using KnowledgeEntity = AgentContext.Domain.Entities.Knowledge;

namespace AgentContext.Application.Tests.Knowledge;

public sealed class KnowledgeAppServiceTests
{
    [Fact]
    public async Task Library_returns_active_items_and_counts_without_duplicate_collections()
    {
        var createdAt = new DateTimeOffset(2026, 8, 21, 10, 0, 0, TimeSpan.Zero);
        var knowledge = new[]
        {
            Knowledge("00000000-0000-0000-0000-000000000001", "alpha", KnowledgeStatus.Active, createdAt),
            Knowledge("00000000-0000-0000-0000-000000000002", "review", KnowledgeStatus.Review, createdAt),
            Knowledge("00000000-0000-0000-0000-000000000003", "archived", KnowledgeStatus.Archived, createdAt),
        };
        var service = CreateService(knowledge);

        var result = await service.ListLibraryAsync(new(Limit: 10));

        Assert.Equal(10, result.Limit);
        Assert.Collection(result.Items, item => Assert.Equal("alpha", item.Title));
        Assert.Equal(new KnowledgeLibraryCounts(1, 1, 1), result.Counts);
        Assert.False(result.HasMore);
        Assert.Null(result.NextCursor);
    }

    [Fact]
    public async Task Library_cursor_traversal_is_stable_for_equal_created_timestamps()
    {
        var createdAt = new DateTimeOffset(2026, 8, 21, 10, 0, 0, TimeSpan.Zero);
        var knowledge = Enumerable.Range(1, 5)
            .Select(index => Knowledge(
                $"00000000-0000-0000-0000-{index:000000000000}",
                $"item-{index}",
                KnowledgeStatus.Active,
                createdAt))
            .ToArray();
        var service = CreateService(knowledge);

        var first = await service.ListLibraryAsync(new(Limit: 2));
        var second = await service.ListLibraryAsync(new(Limit: 2, Cursor: first.NextCursor));
        var final = await service.ListLibraryAsync(new(Limit: 2, Cursor: second.NextCursor));

        Assert.Equal(["item-5", "item-4"], first.Items.Select(item => item.Title));
        Assert.Equal(["item-3", "item-2"], second.Items.Select(item => item.Title));
        Assert.Equal(["item-1"], final.Items.Select(item => item.Title));
        Assert.True(first.HasMore);
        Assert.True(second.HasMore);
        Assert.False(final.HasMore);
        Assert.Empty(first.Items.Select(item => item.Id).Intersect(second.Items.Select(item => item.Id)));
        Assert.Equal(first.NextCursor, second.Cursor);
        Assert.Equal(second.NextCursor, final.Cursor);
    }

    [Fact]
    public async Task Library_review_sort_uses_confidence_then_updated_at()
    {
        var updatedAt = new DateTimeOffset(2026, 8, 21, 10, 0, 0, TimeSpan.Zero);
        var knowledge = new[]
        {
            Knowledge("00000000-0000-0000-0000-000000000001", "later", KnowledgeStatus.Review, updatedAt.AddMinutes(1), 0.2),
            Knowledge("00000000-0000-0000-0000-000000000002", "first", KnowledgeStatus.Review, updatedAt, 0.2),
            Knowledge("00000000-0000-0000-0000-000000000003", "highest", KnowledgeStatus.Review, updatedAt, 0.9),
        };
        var service = CreateService(knowledge);

        var result = await service.ListLibraryAsync(new(KnowledgeStatus.Review, Limit: 10));

        Assert.Equal(["first", "later", "highest"], result.Items.Select(item => item.Title));
        Assert.Equal(0.5, result.ReviewThreshold);
    }

    [Fact]
    public async Task Library_search_is_normalized_and_includes_domain_and_source_task()
    {
        var domain = new DomainEntity { Id = Guid.NewGuid(), Name = "Platform", WorkspaceId = Guid.NewGuid() };
        var session = new Session { Id = Guid.NewGuid(), Task = "Rotate production credentials" };
        var item = Knowledge(
            "00000000-0000-0000-0000-000000000001",
            "Unrelated title",
            KnowledgeStatus.Active,
            DateTimeOffset.UtcNow,
            domain: domain,
            session: session);
        var service = CreateService([item]);

        var result = await service.ListLibraryAsync(new(Search: "  PRODUCTION CREDENTIALS "));

        var matched = Assert.Single(result.Items);
        Assert.Equal(item.Id, matched.Id);
        Assert.Equal("Platform", matched.DomainName);
        Assert.Equal("Rotate production credentials", matched.SourceSessionTask);
    }

    [Fact]
    public async Task Library_rejects_a_cursor_created_for_another_status_or_search()
    {
        var item = Knowledge(
            "00000000-0000-0000-0000-000000000001",
            "alpha",
            KnowledgeStatus.Active,
            DateTimeOffset.UtcNow);
        var secondItem = Knowledge(
            "00000000-0000-0000-0000-000000000002",
            "alpha-second",
            KnowledgeStatus.Active,
            DateTimeOffset.UtcNow.AddMinutes(-1));
        var service = CreateService([item, secondItem]);
        var first = await service.ListLibraryAsync(new(Limit: 1, Search: "alpha"));

        var exception = await Assert.ThrowsAsync<LocalizedException>(() => service.ListLibraryAsync(
            new(KnowledgeStatus.Review, Limit: 1, Cursor: first.NextCursor, Search: "alpha")));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal(ErrorCodes.Knowledge.CursorInvalid, exception.ErrorCode);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task Library_rejects_limits_outside_the_bounded_range(int limit)
    {
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<LocalizedException>(() => service.ListLibraryAsync(new(Limit: limit)));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal(ErrorCodes.Knowledge.PageSizeInvalid, exception.ErrorCode);
    }

    [Fact]
    public async Task Library_rejects_a_malformed_cursor()
    {
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<LocalizedException>(() => service.ListLibraryAsync(new(Cursor: "not-a-cursor")));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal(ErrorCodes.Knowledge.CursorInvalid, exception.ErrorCode);
    }

    private static KnowledgeAppService CreateService(IEnumerable<KnowledgeEntity>? knowledge = null)
        => new(MockKnowledgeDbContext.Create(knowledge).Object);

    private static KnowledgeEntity Knowledge(
        string id,
        string title,
        KnowledgeStatus status,
        DateTimeOffset createdAt,
        double confidence = 0.8,
        DomainEntity? domain = null,
        Session? session = null)
        => new KnowledgeEntity
        {
            Id = Guid.Parse(id),
            WorkspaceId = domain?.WorkspaceId ?? Guid.NewGuid(),
            DomainId = domain?.Id ?? Guid.NewGuid(),
            Type = KnowledgeType.Solution,
            Title = title,
            Content = $"Content for {title}",
            Confidence = confidence,
            Status = status,
            CreatedAtUtc = createdAt,
            UpdatedAtUtc = createdAt,
            Domain = domain ?? new DomainEntity { Id = Guid.NewGuid(), Name = "General", WorkspaceId = Guid.NewGuid() },
            SourceSessionId = session?.Id,
            SourceSession = session,
        };
}
