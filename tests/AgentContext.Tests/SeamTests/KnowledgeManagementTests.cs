using AgentContext.Application.KnowledgeManagement;
using AgentContext.Application.Learning;
using AgentContext.Application.Retrieval;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using AgentContext.Tests.Fakes;
using AgentContext.Tests.Testcontainers;
using Pgvector;
using Microsoft.EntityFrameworkCore;
using DomainEntity = AgentContext.Domain.Entities.Domain;

namespace AgentContext.Tests.SeamTests;

/// <summary>
/// Knowledge management (issue #6): the list with Confidence + provenance, the
/// sub-threshold review list, the private marker, deletion (AC4), and
/// rate_knowledge — the dynamic Confidence feedback loop (AC3, spec §6.3).
/// </summary>
public sealed class KnowledgeManagementTests : PostgresTestBase
{
    private async Task<(AgentContextDbContext Db, Guid AlphaId, Guid LowId, Guid SessionId)> SeededAsync()
    {
        var db = Fixture.CreateDbContext();
        await db.Database.MigrateAsync();
        var workspace = new Workspace { Name = "W", Type = WorkspaceType.Personal };
        var domain = new DomainEntity { WorkspaceId = workspace.Id, Name = "dev", IsShared = false };
        var session = new Session
        {
            WorkspaceId = workspace.Id, AgentName = "a", Task = "Fix pgvector index",
            Conclusion = "c", SummaryJson = "{}", Status = SessionStatus.Completed,
        };
        db.Workspaces.Add(workspace);
        db.Domains.Add(domain);
        db.Sessions.Add(session);
        db.Knowledge.AddRange(
            new Knowledge
            {
                WorkspaceId = workspace.Id, DomainId = domain.Id, SourceSessionId = session.Id,
                Type = KnowledgeType.Solution, Title = "Alpha", Content = "alpha",
                Confidence = 0.8, Status = KnowledgeStatus.Active,
                Embedding = new Vector(FakeLlmClient.VectorFor("alpha")),
            },
            new Knowledge
            {
                WorkspaceId = workspace.Id, DomainId = domain.Id, SourceSessionId = session.Id,
                Type = KnowledgeType.Pattern, Title = "Low", Content = "low",
                Confidence = 0.3, Status = KnowledgeStatus.Active,
                Embedding = new Vector(FakeLlmClient.VectorFor("low")),
            });
        await db.SaveChangesAsync();
        var ids = await db.Knowledge.AsNoTracking().Select(k => new { k.Title, k.Id }).ToListAsync();
        return (db, ids.Single(i => i.Title == "Alpha").Id, ids.Single(i => i.Title == "Low").Id, session.Id);
    }

    private static KnowledgeAppService Service(AgentContextDbContext db) => new(db);

    [Fact]
    public async Task List_returns_active_knowledge_with_confidence_and_provenance()
    {
        var (db, alphaId, _, sessionId) = await SeededAsync();

        var items = await Service(db).ListAsync();

        var alpha = Assert.Single(items, i => i.Id == alphaId);
        Assert.Equal(0.8, alpha.Confidence);
        Assert.Equal("dev", alpha.DomainName);
        Assert.Equal("Fix pgvector index", alpha.SourceSessionTask);
        Assert.Equal(KnowledgeType.Solution, alpha.Type);
    }

    [Fact]
    public async Task ListReview_returns_only_review_status_items()
    {
        var (db, _, _, _) = await SeededAsync();
        // T8: Review is an explicit status; the low-confidence Active item stays
        // in the main list until hygiene moves it. Move one item into Review.
        var lowId = await db.Knowledge.AsNoTracking()
            .Where(k => k.Title == "Low")
            .Select(k => k.Id)
            .SingleAsync();
        await db.Knowledge.Where(k => k.Id == lowId)
            .ExecuteUpdateAsync(s => s.SetProperty(k => k.Status, KnowledgeStatus.Review));

        var review = await Service(db).ListReviewAsync();

        Assert.Equal(RetrievalDefaults.MinConfidence, review.Threshold);
        var item = Assert.Single(review.Items);
        Assert.Equal(lowId, item.Id);
        Assert.True(item.Confidence < review.Threshold);
    }

    [Fact]
    public async Task ListArchived_returns_only_archived_items()
    {
        var (db, alphaId, _, _) = await SeededAsync();
        await db.Knowledge.Where(k => k.Id == alphaId)
            .ExecuteUpdateAsync(s => s.SetProperty(k => k.Status, KnowledgeStatus.Archived));

        var archived = await Service(db).ListArchivedAsync();

        var item = Assert.Single(archived);
        Assert.Equal(alphaId, item.Id);
    }

    [Fact]
    public async Task UpdateVisibility_toggles_the_private_marker()
    {
        var (db, alphaId, _, _) = await SeededAsync();

        await Service(db).UpdateVisibilityAsync(alphaId, isPrivate: true);

        Assert.True(await db.Knowledge.AsNoTracking().Where(k => k.Id == alphaId).Select(k => k.IsPrivate).SingleAsync());
    }

    [Fact]
    public async Task UpdateVisibility_throws_for_unknown_id()
    {
        var (db, _, _, _) = await SeededAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(db).UpdateVisibilityAsync(Guid.NewGuid(), true));
    }

    [Fact]
    public async Task Delete_removes_the_item_from_the_store_and_retrieval()
    {
        var (db, alphaId, _, _) = await SeededAsync();
        var retrieval = new RetrievalAppService(db, new FakeLlmClientShim());

        // AC4: before deletion the item is retrievable; after deletion it is gone.
        var before = await retrieval.SearchMemoryAsync("dev", "alpha");
        Assert.Contains(before.Items, i => i.Id == alphaId);

        await Service(db).DeleteAsync(alphaId);

        Assert.Equal(0, await db.Knowledge.CountAsync(k => k.Id == alphaId));
        var after = await retrieval.SearchMemoryAsync("dev", "alpha");
        Assert.DoesNotContain(after.Items, i => i.Id == alphaId);
    }

    [Fact]
    public async Task Delete_throws_for_unknown_id()
    {
        var (db, _, _, _) = await SeededAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(db).DeleteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Rate_useful_bumps_confidence_by_0_1_capped_at_1_0()
    {
        var (db, alphaId, _, _) = await SeededAsync();

        var first = await Service(db).RateAsync(alphaId, useful: true);
        Assert.Equal(0.9, first.Confidence, 3);
        Assert.Equal(alphaId, first.Id);
        Assert.Equal(0.9, await db.Knowledge.AsNoTracking().Where(k => k.Id == alphaId).Select(k => k.Confidence).SingleAsync(), 3);

        // second bump 0.9 → 1.0 (capped)
        var second = await Service(db).RateAsync(alphaId, useful: true);
        Assert.Equal(1.0, second.Confidence, 3);
    }

    [Fact]
    public async Task Rate_not_useful_clears_confidence_and_lands_in_review()
    {
        var (db, alphaId, _, _) = await SeededAsync();

        var result = await Service(db).RateAsync(alphaId, useful: false);

        Assert.Equal(0, result.Confidence);
        var review = await Service(db).ListReviewAsync();
        Assert.Contains(review.Items, i => i.Id == alphaId);
    }

    [Fact]
    public async Task Rate_throws_for_unknown_id()
    {
        var (db, _, _, _) = await SeededAsync();

        await Assert.ThrowsAsync<KeyNotFoundException>(() => Service(db).RateAsync(Guid.NewGuid(), useful: true));
    }

    private sealed class FakeLlmClientShim : AgentContext.Application.Contracts.ILlmClient
    {
        public Task<IReadOnlyList<AgentContext.Application.Dtos.KnowledgeExtraction>> ExtractKnowledgeAsync(
            string sessionSummaryJson, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<AgentContext.Application.Dtos.KnowledgeExtraction>>([]);

        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
            => Task.FromResult(FakeLlmClient.VectorFor(text));
    }
}
