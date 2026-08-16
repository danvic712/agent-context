using AgentContext.Application.Hygiene;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using AgentContext.Tests.Testcontainers;
using Microsoft.EntityFrameworkCore;
using DomainEntity = AgentContext.Domain.Entities.Domain;

namespace AgentContext.Tests.SeamTests;

/// <summary>
/// Knowledge hygiene (T8, issue #9 / spec US20): temporal decay moves long-unused,
/// low-Confidence items to Review, Review items archive after a grace period, and
/// the whole pass is idempotent (AC2). Primary seam against Testcontainers pgvector.
/// </summary>
public sealed class KnowledgeHygieneTests : PostgresTestBase
{
    private async Task<(AgentContextDbContext Db, Guid WorkspaceId, Guid DomainId)> SeededAsync()
    {
        var db = Fixture.CreateDbContext();
        await db.Database.MigrateAsync();
        var workspace = new Workspace { Name = "W", Type = WorkspaceType.Personal };
        var domain = new DomainEntity { WorkspaceId = workspace.Id, Name = "dev", IsShared = false };
        db.Workspaces.Add(workspace);
        db.Domains.Add(domain);
        await db.SaveChangesAsync();
        return (db, workspace.Id, domain.Id);
    }

    private static KnowledgeHygieneAppService Service(AgentContextDbContext db) => new(db);

    [Fact]
    public async Task Long_unused_active_item_decays_but_stays_active_above_threshold()
    {
        var (db, workspaceId, domainId) = await SeededAsync();
        db.Knowledge.Add(new Knowledge
        {
            WorkspaceId = workspaceId, DomainId = domainId,
            Type = KnowledgeType.Solution, Title = "Stale", Content = "c",
            Confidence = 0.9, Status = KnowledgeStatus.Active,
            // Never used, created long ago: 2 full decay windows → decay 0.2.
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-70),
            UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-70),
        });
        await db.SaveChangesAsync();

        var result = await Service(db).RunOnceAsync();

        Assert.Equal(1, result.Decayed);
        Assert.Equal(0, result.MovedToReview);

        var item = await db.Knowledge.AsNoTracking().SingleAsync();
        Assert.Equal(KnowledgeStatus.Active, item.Status);
        Assert.Equal(0.7, item.Confidence, 3);
    }

    [Fact]
    public async Task Decayed_below_threshold_moves_to_review()
    {
        var (db, workspaceId, domainId) = await SeededAsync();
        db.Knowledge.Add(new Knowledge
        {
            WorkspaceId = workspaceId, DomainId = domainId,
            Type = KnowledgeType.Solution, Title = "Weak", Content = "c",
            Confidence = 0.6, Status = KnowledgeStatus.Active,
            // 4 windows × 0.1 = 0.4 decay → 0.2 < 0.5 → Review.
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-130),
            UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-130),
        });
        await db.SaveChangesAsync();

        var result = await Service(db).RunOnceAsync();

        Assert.Equal(1, result.Decayed);
        Assert.Equal(1, result.MovedToReview);

        var item = await db.Knowledge.AsNoTracking().SingleAsync();
        Assert.Equal(KnowledgeStatus.Review, item.Status);
        Assert.Equal(0.2, item.Confidence, 3);
    }

    [Fact]
    public async Task Recently_used_item_does_not_decay()
    {
        var (db, workspaceId, domainId) = await SeededAsync();
        db.Knowledge.Add(new Knowledge
        {
            WorkspaceId = workspaceId, DomainId = domainId,
            Type = KnowledgeType.Solution, Title = "Fresh", Content = "c",
            Confidence = 0.6, Status = KnowledgeStatus.Active,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-100),
            LastUsedAtUtc = DateTimeOffset.UtcNow.AddDays(-1), // used yesterday
        });
        await db.SaveChangesAsync();

        var result = await Service(db).RunOnceAsync();

        Assert.Equal(0, result.Decayed);
        Assert.Equal(0, result.MovedToReview);
        Assert.Equal(0.6, await db.Knowledge.AsNoTracking().Select(k => k.Confidence).SingleAsync());
    }

    [Fact]
    public async Task Review_item_untouched_past_grace_period_is_archived()
    {
        var (db, workspaceId, domainId) = await SeededAsync();
        db.Knowledge.Add(new Knowledge
        {
            WorkspaceId = workspaceId, DomainId = domainId,
            Type = KnowledgeType.Solution, Title = "Reviewing", Content = "c",
            Confidence = 0.3, Status = KnowledgeStatus.Review,
            UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-10), // beyond 7-day grace
        });
        await db.SaveChangesAsync();

        var result = await Service(db).RunOnceAsync();

        Assert.Equal(0, result.Decayed);
        Assert.Equal(1, result.Archived);
        Assert.Equal(KnowledgeStatus.Archived,
            await db.Knowledge.AsNoTracking().Select(k => k.Status).SingleAsync());
    }

    [Fact]
    public async Task Fresh_review_item_stays_in_review()
    {
        var (db, workspaceId, domainId) = await SeededAsync();
        db.Knowledge.Add(new Knowledge
        {
            WorkspaceId = workspaceId, DomainId = domainId,
            Type = KnowledgeType.Solution, Title = "New review", Content = "c",
            Confidence = 0.3, Status = KnowledgeStatus.Review,
            UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-1),
        });
        await db.SaveChangesAsync();

        var result = await Service(db).RunOnceAsync();

        Assert.Equal(0, result.Archived);
        Assert.Equal(KnowledgeStatus.Review,
            await db.Knowledge.AsNoTracking().Select(k => k.Status).SingleAsync());
    }

    [Fact]
    public async Task Decay_that_stays_active_is_idempotent_too()
    {
        var (db, workspaceId, domainId) = await SeededAsync();
        db.Knowledge.Add(new Knowledge
        {
            WorkspaceId = workspaceId, DomainId = domainId,
            Type = KnowledgeType.Solution, Title = "Strong", Content = "c",
            Confidence = 0.9, Status = KnowledgeStatus.Active,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-70),
            UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-70),
        });
        await db.SaveChangesAsync();
        var service = Service(db);

        var first = await service.RunOnceAsync();
        Assert.Equal(1, first.Decayed);
        Assert.Equal(0, first.MovedToReview); // 0.9 → 0.7, still ≥ 0.5

        // Second run must not decay it again (AC2) — UpdatedAtUtc now tracks the decay.
        var second = await service.RunOnceAsync();
        Assert.Equal(0, second.Decayed);
        Assert.Equal(0.7, await db.Knowledge.AsNoTracking().Select(k => k.Confidence).SingleAsync(), 3);
    }

    [Fact]
    public async Task Run_is_idempotent()
    {
        var (db, workspaceId, domainId) = await SeededAsync();
        db.Knowledge.Add(new Knowledge
        {
            WorkspaceId = workspaceId, DomainId = domainId,
            Type = KnowledgeType.Solution, Title = "Stale", Content = "c",
            Confidence = 0.6, Status = KnowledgeStatus.Active,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-130),
            UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-130),
        });
        await db.SaveChangesAsync();
        var service = Service(db);

        var first = await service.RunOnceAsync();
        Assert.Equal(1, first.MovedToReview);

        // Second run over the same data must change nothing.
        var second = await service.RunOnceAsync();
        Assert.Equal(0, second.Decayed);
        Assert.Equal(0, second.MovedToReview);
        Assert.Equal(0, second.Archived);

        var item = await db.Knowledge.AsNoTracking().SingleAsync();
        Assert.Equal(KnowledgeStatus.Review, item.Status);
        Assert.Equal(0.2, item.Confidence, 3);
    }

    [Fact]
    public async Task Run_is_idempotent_for_archiving_too()
    {
        var (db, workspaceId, domainId) = await SeededAsync();
        db.Knowledge.Add(new Knowledge
        {
            WorkspaceId = workspaceId, DomainId = domainId,
            Type = KnowledgeType.Solution, Title = "Old review", Content = "c",
            Confidence = 0.3, Status = KnowledgeStatus.Review,
            UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-10),
        });
        await db.SaveChangesAsync();
        var service = Service(db);

        Assert.Equal(1, (await service.RunOnceAsync()).Archived);
        var second = await service.RunOnceAsync();
        Assert.Equal(0, second.Archived);
        Assert.Equal(0, second.Decayed);
    }

    [Fact]
    public async Task Active_item_never_used_decays_from_created_at()
    {
        var (db, workspaceId, domainId) = await SeededAsync();
        db.Knowledge.Add(new Knowledge
        {
            WorkspaceId = workspaceId, DomainId = domainId,
            Type = KnowledgeType.Pattern, Title = "Old pattern", Content = "c",
            Confidence = 0.8, Status = KnowledgeStatus.Active,
            CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-31),
            UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-31),
        });
        await db.SaveChangesAsync();

        var result = await Service(db).RunOnceAsync();

        Assert.Equal(1, result.Decayed);
        Assert.Equal(0.7, await db.Knowledge.AsNoTracking().Select(k => k.Confidence).SingleAsync(), 3);
        Assert.Equal(KnowledgeStatus.Active, await db.Knowledge.AsNoTracking().Select(k => k.Status).SingleAsync());
    }
}
