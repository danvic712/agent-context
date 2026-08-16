using AgentContext.Application.Learning;
using AgentContext.Application.Retrieval;
using AgentContext.Application.Settings;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using AgentContext.Tests.Fakes;
using AgentContext.Tests.Testcontainers;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using DomainEntity = AgentContext.Domain.Entities.Domain;

namespace AgentContext.Tests.SeamTests;

/// <summary>
/// Shared retrieval backend (issue #5, spec US 17/18/19): search_memory and
/// find_similar_solution query pgvector — domain-scoped, Confidence ≥ threshold
/// (default 0.5), conflict pairs surfaced side by side, private items visible to
/// the single-user owner (MVP).
/// </summary>
public sealed class RetrievalTests : PostgresTestBase
{
    private async Task<(AgentContextDbContext Db, Guid Dev, Guid Home)> SeedAsync(params (string Domain, Knowledge Knowledge)[] items)
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
        var dev = new DomainEntity { WorkspaceId = workspace.Id, Name = "dev", IsShared = false };
        var home = new DomainEntity { WorkspaceId = workspace.Id, Name = "home", IsShared = false };
        db.Workspaces.Add(workspace);
        db.Domains.AddRange(dev, home);

        foreach (var (domain, knowledge) in items)
        {
            knowledge.WorkspaceId = workspace.Id;
            knowledge.DomainId = domain == "dev" ? dev.Id : home.Id;
            db.Knowledge.Add(knowledge);
        }

        await db.SaveChangesAsync();
        return (db, dev.Id, home.Id);
    }

    private static (string Domain, Knowledge Knowledge) In(string domain, string content,
        KnowledgeType type = KnowledgeType.Solution, double confidence = 0.8,
        bool isPrivate = false, string? groupId = null, Vector? embedding = null)
        => (domain, new Knowledge
        {
            Type = type,
            Title = $"Title {content}",
            Content = content,
            Confidence = confidence,
            Embedding = embedding ?? new Vector(FakeLlmClient.VectorFor(content)),
            Status = KnowledgeStatus.Active,
            IsPrivate = isPrivate,
            ConflictGroupId = groupId,
        });

    /// <summary>Unit vector with the given cosine to <see cref="UnitVector(0)"/>: (c, √(1−c²), 0, …).</summary>
    private static Vector CosineUnit(double c)
    {
        var v = new float[LearningPipelineDefaults.EmbeddingDimensions];
        v[0] = (float)c;
        v[1] = (float)Math.Sqrt(1 - c * c);
        return new Vector(v);
    }

    private static RetrievalAppService Retrieval(AgentContextDbContext db, FakeLlmClient? llm = null) =>
        new(db, llm ?? new FakeLlmClient());

    [Fact]
    public async Task SearchMemory_returns_knowledge_ranked_by_similarity_within_the_domain_only()
    {
        var (db, _, _) = await SeedAsync(
            In("dev", "alpha"),
            In("dev", "beta"),
            In("home", "alpha-home")); // same content family, must NOT leak into dev
        var alpha = await db.Knowledge.AsNoTracking().SingleAsync(k => k.Content == "alpha");
        var homeAlpha = await db.Knowledge.AsNoTracking().SingleAsync(k => k.Content == "alpha-home");

        var result = await Retrieval(db).SearchMemoryAsync("dev", "alpha");

        Assert.Contains(result.Items, i => i.Id == alpha.Id);
        Assert.Equal(alpha.Id, result.Items[0].Id); // exact match ranks first
        Assert.True(result.Items[0].Score > 0.9); // near-identical embedding
        Assert.DoesNotContain(result.Items, i => i.Id == homeAlpha.Id); // domain-scoped
    }

    [Fact]
    public async Task SearchMemory_filters_below_threshold_confidence_by_default_and_allows_override()
    {
        var (db, _, _) = await SeedAsync(
            In("dev", "alpha", confidence: 0.8),
            In("dev", "gamma", confidence: 0.4));

        var defaultResult = await Retrieval(db).SearchMemoryAsync("dev", "alpha");
        Assert.DoesNotContain(defaultResult.Items, i => i.Confidence < RetrievalDefaults.MinConfidence);

        var lowered = await Retrieval(db).SearchMemoryAsync("dev", "alpha", minConfidence: 0.3);
        Assert.Contains(lowered.Items, i => i.Confidence == 0.4);
    }

    [Fact]
    public async Task SearchMemory_surfaces_conflict_group_partners_side_by_side()
    {
        var groupId = Guid.CreateVersion7().ToString();
        var (db, _, _) = await SeedAsync(
            In("dev", "alpha", groupId: groupId),
            In("dev", "beta-far", groupId: groupId)); // same group, far from the query
        var alpha = await db.Knowledge.AsNoTracking().SingleAsync(k => k.Content == "alpha");
        var beta = await db.Knowledge.AsNoTracking().SingleAsync(k => k.Content == "beta-far");

        var result = await Retrieval(db).SearchMemoryAsync("dev", "alpha");

        Assert.Contains(result.Items, i => i.Id == alpha.Id);
        Assert.Contains(result.Items, i => i.Id == beta.Id); // partner surfaced side by side
        Assert.Equal(2, result.Items.Count); // no duplicate
    }

    [Fact]
    public async Task SearchMemory_is_empty_for_an_unknown_domain()
    {
        var (db, _, _) = await SeedAsync(In("dev", "alpha"));

        var result = await Retrieval(db).SearchMemoryAsync("nope", "alpha");

        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task FindSimilarSolution_returns_best_solution_with_conflicts_and_ignores_other_types()
    {
        var groupId = Guid.CreateVersion7().ToString();
        var (db, _, _) = await SeedAsync(
            In("dev", "alpha", type: KnowledgeType.Solution, groupId: groupId),
            In("dev", "alpha", type: KnowledgeType.Problem, groupId: groupId), // same content, wrong type
            In("dev", "alpha-opposing", type: KnowledgeType.Solution, groupId: groupId)); // conflict partner
        var solution = await db.Knowledge.AsNoTracking().SingleAsync(k => k.Content == "alpha" && k.Type == KnowledgeType.Solution);
        var partner = await db.Knowledge.AsNoTracking().SingleAsync(k => k.Content == "alpha-opposing");

        var result = await Retrieval(db).FindSimilarSolutionAsync("dev", "alpha");

        Assert.NotNull(result.Solution);
        Assert.Equal(solution.Id, result.Solution!.Id);
        Assert.DoesNotContain(result.Conflicts, c => c.Type == KnowledgeType.Problem);
        Assert.Contains(result.Conflicts, c => c.Id == partner.Id);
    }

    [Fact]
    public async Task FindSimilarSolution_returns_null_when_no_solution_meets_the_threshold()
    {
        var (db, _, _) = await SeedAsync(
            In("dev", "alpha", type: KnowledgeType.Problem, confidence: 0.8),
            In("dev", "gamma", type: KnowledgeType.Solution, confidence: 0.4));

        var result = await Retrieval(db).FindSimilarSolutionAsync("dev", "alpha");

        Assert.Null(result.Solution);
        Assert.Empty(result.Conflicts);
    }

    [Fact]
    public async Task SearchMemory_conflict_partners_bypass_the_threshold_for_judgement()
    {
        // US19: the low-confidence side of a conflict must still surface (that is
        // exactly what needs judgement) — partners are appended with Score = 0.
        // Nine filler rows (distance 0.5) push the partner (distance 0.9) out of
        // the Top-N ranked set, so it can only come back via the group append.
        var groupId = Guid.CreateVersion7().ToString();
        var items = new List<(string Domain, Knowledge Knowledge)>
        {
            In("dev", "alpha", confidence: 0.9, groupId: groupId, embedding: CosineUnit(1)),
        };
        for (var i = 0; i < 9; i++)
        {
            items.Add(In("dev", $"filler{i}", confidence: 0.9, embedding: CosineUnit(0.5)));
        }

        items.Add(In("dev", "alpha-opposing", confidence: 0.3, groupId: groupId, embedding: CosineUnit(0.1)));
        var (db, _, _) = await SeedAsync(items.ToArray());
        var llm = new FakeLlmClient { EmbeddingFor = _ => CosineUnit(1).ToArray() };

        var result = await Retrieval(db, llm).SearchMemoryAsync("dev", "alpha");

        var partner = Assert.Single(result.Items, i => i.Content == "alpha-opposing");
        Assert.Equal(0.3, partner.Confidence); // below the default 0.5 threshold
        Assert.Equal(0, partner.Score);        // attached, not ranked
    }

    [Fact]
    public async Task Retrieval_includes_private_items_for_the_single_user_owner()
    {
        // AC5: single-user MVP — the owner always sees their own (private) items.
        var (db, _, _) = await SeedAsync(In("dev", "alpha", isPrivate: true));
        var alpha = await db.Knowledge.AsNoTracking().SingleAsync();

        var result = await Retrieval(db).SearchMemoryAsync("dev", "alpha");

        Assert.Contains(result.Items, i => i.Id == alpha.Id && i.IsPrivate);
    }
}
