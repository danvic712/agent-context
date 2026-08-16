using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Application.Enums;
using AgentContext.Application.Learning;
using AgentContext.Application.Settings;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using AgentContext.Tests.Fakes;
using AgentContext.Tests.Testcontainers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using DomainEntity = AgentContext.Domain.Entities.Domain;

namespace AgentContext.Tests.SeamTests;

/// <summary>
/// Conflict detection at insert time (issue #5 AC4, spec §6.3): Knowledge that is
/// semantically related to existing Knowledge — similar but not a duplicate —
/// is marked as a conflict pair (shared ConflictGroupId) so retrieval can show
/// both sides. Distinct from dedup (≥ 0.9 → corroborate) and unrelated (< 0.6 →
/// standalone).
/// </summary>
public sealed class ConflictDetectionTests : PostgresTestBase
{
    private const string LongContent =
        "A concrete fix: configure the connection string with a thirty second timeout and retry with " +
        "exponential backoff so transient database outages never surface to callers.";

    /// <summary>Cosine similarity to <see cref="VecNearUnit0"/> is exactly 0.8 (conflict band [0.6, 0.9)).</summary>
    private static readonly float[] VecUnit0 = CreateUnitVector(0);

    private static readonly float[] VecNearUnit0 = CreateNearVector();

    /// <summary>
    /// Cosine similarity 0.8 to VecNearUnit0 (so "C" conflicts with "B" — its
    /// nearest neighbour — rather than being deduped) and 0.64 to VecUnit0.
    /// </summary>
    private static readonly float[] VecNearUnit0Two = CreateNearVector2();

    /// <summary>Cosine similarity 0 to the vectors above (standalone band).</summary>
    private static readonly float[] VecFar = CreateUnitVector(2);

    private async Task<(AgentContextDbContext Db, Guid First, Guid Second, Guid Third)> SeedThreeSessionsAsync()
    {
        var db = Fixture.CreateDbContext();
        await db.Database.MigrateAsync();
        await new SettingsAppService(db).SaveLlmOptionsAsync(new LlmOptions
        {
            BaseUrl = "http://localhost:11434/v1",
            ApiKey = "test-key",
            Model = "llama3.2",
        });
        var workspace = new Workspace { Name = "W", Type = WorkspaceType.Personal };
        var domain = new DomainEntity { WorkspaceId = workspace.Id, Name = "dev", IsShared = false };
        db.Workspaces.Add(workspace);
        db.Domains.Add(domain);
        var sessions = Enumerable.Range(0, 3).Select(i => new Session
        {
            WorkspaceId = workspace.Id, DomainId = domain.Id, AgentName = "a",
            Task = $"t{i}", Conclusion = "c", SummaryJson = "{}", Status = SessionStatus.Pending,
        }).ToList();
        db.Sessions.AddRange(sessions);
        await db.SaveChangesAsync();
        return (db, sessions[0].Id, sessions[1].Id, sessions[2].Id);
    }

    private static LearningPipelineAppService Pipeline(AgentContextDbContext db, FakeLlmClient llm) =>
        new(db, llm, new SettingsAppService(db), NullLogger<LearningPipelineAppService>.Instance);

    private static FakeLlmClient ConflictAwareLlm() => new()
    {
        EmbeddingFor = text => text switch
        {
            $"A\n{LongContent}" => VecUnit0,
            $"B\n{LongContent}" => VecNearUnit0,
            $"C\n{LongContent}" => VecNearUnit0Two,
            _ => VecFar,
        },
    };

    private static float[] CreateUnitVector(int index)
    {
        var v = new float[LearningPipelineDefaults.EmbeddingDimensions];
        v[index] = 1f;
        return v;
    }

    private static float[] CreateNearVector()
    {
        var v = new float[LearningPipelineDefaults.EmbeddingDimensions];
        v[0] = 0.8f;
        v[1] = 0.6f;
        return v;
    }

    /// <summary>(0.64, 0.48, 0.6) — unit length; dot with VecNearUnit0 = 0.8, with VecUnit0 = 0.64.</summary>
    private static float[] CreateNearVector2()
    {
        var v = new float[LearningPipelineDefaults.EmbeddingDimensions];
        v[0] = 0.64f;
        v[1] = 0.48f;
        v[2] = 0.6f;
        return v;
    }

    [Fact]
    public async Task ProcessAsync_related_but_distinct_content_marks_a_conflict_pair()
    {
        var (db, firstSession, secondSession, _) = await SeedThreeSessionsAsync();
        var llm = ConflictAwareLlm();
        llm.EnqueueExtractions(new KnowledgeExtraction(KnowledgeType.Solution, "A", LongContent, 0.8));
        var first = await Pipeline(db, llm).ProcessAsync(firstSession);
        Assert.Equal(1, first.KnowledgeCreated);

        llm.EnqueueExtractions(new KnowledgeExtraction(KnowledgeType.Solution, "B", LongContent, 0.8));
        var second = await Pipeline(db, llm).ProcessAsync(secondSession);

        Assert.Equal(PipelineOutcome.Completed, second.Outcome);
        Assert.Equal(1, second.KnowledgeCreated); // a conflict is a real row, not a dedup
        Assert.Equal(0, second.KnowledgeCorroborated);

        var rows = await db.Knowledge.AsNoTracking().OrderBy(k => k.Title).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.NotNull(rows[0].ConflictGroupId);
        Assert.Equal(rows[0].ConflictGroupId, rows[1].ConflictGroupId);
    }

    [Fact]
    public async Task ProcessAsync_conflict_joins_the_existing_group()
    {
        // A standalone → B conflicts with A (new group) → C conflicts with B and
        // inherits B's group, so all three share one ConflictGroupId.
        var (db, firstSession, secondSession, thirdSession) = await SeedThreeSessionsAsync();
        var llm = ConflictAwareLlm();
        llm.EnqueueExtractions(new KnowledgeExtraction(KnowledgeType.Solution, "A", LongContent, 0.8));
        await Pipeline(db, llm).ProcessAsync(firstSession);
        llm.EnqueueExtractions(new KnowledgeExtraction(KnowledgeType.Solution, "B", LongContent, 0.8));
        await Pipeline(db, llm).ProcessAsync(secondSession);
        llm.EnqueueExtractions(new KnowledgeExtraction(KnowledgeType.Solution, "C", LongContent, 0.8));
        await Pipeline(db, llm).ProcessAsync(thirdSession);

        var rows = await db.Knowledge.AsNoTracking().OrderBy(k => k.Title).ToListAsync();
        Assert.Equal(3, rows.Count);
        Assert.Equal(1, rows.Select(r => r.ConflictGroupId).Distinct().Count());
        Assert.NotNull(rows[0].ConflictGroupId);
    }

    [Fact]
    public async Task ProcessAsync_unrelated_content_stays_standalone()
    {
        var (db, firstSession, secondSession, _) = await SeedThreeSessionsAsync();
        var llm = ConflictAwareLlm();
        llm.EnqueueExtractions(new KnowledgeExtraction(KnowledgeType.Solution, "A", LongContent, 0.8));
        await Pipeline(db, llm).ProcessAsync(firstSession);

        llm.EnqueueExtractions(new KnowledgeExtraction(KnowledgeType.Pattern, "Far item", LongContent, 0.8));
        var result = await Pipeline(db, llm).ProcessAsync(secondSession);

        Assert.Equal(1, result.KnowledgeCreated);
        var rows = await db.Knowledge.AsNoTracking().OrderBy(k => k.Title).ToListAsync();
        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Null(r.ConflictGroupId));
    }
}
