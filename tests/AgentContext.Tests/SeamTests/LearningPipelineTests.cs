using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Application.Learning;
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
/// Primary seam for the Learning Engine (spec §Testing Decisions): the pipeline
/// is invoked synchronously on a recorded Session against a real pgvector
/// Postgres, with the LLM faked (never the database). AC2 — a pending Session
/// produces Knowledge rows with Confidence in pgvector; AC3 — duplicate content
/// corroborates existing Knowledge instead of creating duplicates.
/// </summary>
public sealed class LearningPipelineTests : PostgresTestBase
{
    private const string LongContent =
        "A concrete fix: configure the connection string with a thirty second timeout and retry with " +
        "exponential backoff so transient database outages never surface to callers.";

    private async Task<(AgentContextDbContext Db, Guid SessionId)> SeededPendingSessionAsync(
        bool withDomain = true, string summary = """{"task":"t","conclusion":"c"}""")
    {
        var db = Fixture.CreateDbContext();
        await db.Database.MigrateAsync();

        var workspace = new Workspace { Name = "W", Type = WorkspaceType.Personal };
        db.Workspaces.Add(workspace);
        await db.SaveChangesAsync();

        var session = new Session
        {
            WorkspaceId = workspace.Id,
            AgentName = "a",
            Task = "t",
            Conclusion = "c",
            SummaryJson = summary,
            Status = SessionStatus.Pending,
        };
        if (withDomain)
        {
            var domain = new DomainEntity { WorkspaceId = workspace.Id, Name = "dev", IsShared = false };
            db.Domains.Add(domain);
            session.DomainId = domain.Id;
        }

        db.Sessions.Add(session);
        await db.SaveChangesAsync();

        return (db, session.Id);
    }

    /// <summary>Seeds two pending Sessions sharing one Workspace + Domain (dedup needs the same domain).</summary>
    private async Task<(AgentContextDbContext Db, Guid First, Guid Second)> SeedSharedSessionsAsync()
    {
        var db = Fixture.CreateDbContext();
        await db.Database.MigrateAsync();
        var workspace = new Workspace { Name = "W", Type = WorkspaceType.Personal };
        var domain = new DomainEntity { WorkspaceId = workspace.Id, Name = "dev", IsShared = false };
        db.Workspaces.Add(workspace);
        db.Domains.Add(domain);
        var first = new Session
        {
            WorkspaceId = workspace.Id, DomainId = domain.Id, AgentName = "a",
            Task = "t", Conclusion = "c", SummaryJson = "{}", Status = SessionStatus.Pending,
        };
        var second = new Session
        {
            WorkspaceId = workspace.Id, DomainId = domain.Id, AgentName = "a",
            Task = "t2", Conclusion = "c2", SummaryJson = "{}", Status = SessionStatus.Pending,
        };
        db.Sessions.AddRange(first, second);
        await db.SaveChangesAsync();
        return (db, first.Id, second.Id);
    }

    private static LearningPipelineAppService Pipeline(AgentContextDbContext db, FakeLlmClient llm) =>
        new(db, llm, NullLogger<LearningPipelineAppService>.Instance);

    [Fact]
    public async Task ProcessAsync_creates_knowledge_with_confidence_and_embedding_then_completes_session()
    {
        var (db, sessionId) = await SeededPendingSessionAsync();
        var llm = new FakeLlmClient();
        llm.EnqueueExtractions(new KnowledgeExtraction(KnowledgeType.Solution, "Fix DB timeouts", LongContent, 0.8));

        var result = await Pipeline(db, llm).ProcessAsync(sessionId);

        Assert.Equal(PipelineOutcome.Completed, result.Outcome);
        Assert.Equal(1, result.KnowledgeCreated);
        Assert.Equal(0, result.KnowledgeCorroborated);

        var session = await db.Sessions.AsNoTracking().SingleAsync(s => s.Id == sessionId);
        Assert.Equal(SessionStatus.Completed, session.Status);
        Assert.NotNull(session.ProcessedAtUtc);

        var knowledge = await db.Knowledge.AsNoTracking().SingleAsync();
        Assert.Equal(KnowledgeType.Solution, knowledge.Type);
        Assert.Equal("Fix DB timeouts", knowledge.Title);
        Assert.Equal(LongContent, knowledge.Content);
        // Confidence = 0.4 × selfAssessment + 0.2 × completeness (title + content + length ≥ 80) → 0.4×0.8 + 0.2×1.0
        Assert.Equal(0.52, knowledge.Confidence, 3);
        Assert.Equal(sessionId, knowledge.SourceSessionId);
        Assert.Equal(KnowledgeStatus.Active, knowledge.Status);
        Assert.NotNull(knowledge.Embedding);
        Assert.Equal(FakeLlmClient.VectorFor($"Fix DB timeouts\n{LongContent}"), knowledge.Embedding!.ToArray());
    }

    [Fact]
    public async Task ProcessAsync_caps_confidence_at_0_6()
    {
        var (db, sessionId) = await SeededPendingSessionAsync();
        var llm = new FakeLlmClient();
        llm.EnqueueExtractions(new KnowledgeExtraction(KnowledgeType.Pattern, "Perfect item", LongContent, 1.0));

        var result = await Pipeline(db, llm).ProcessAsync(sessionId);

        Assert.Equal(PipelineOutcome.Completed, result.Outcome);
        var knowledge = await db.Knowledge.AsNoTracking().SingleAsync();
        Assert.Equal(0.6, knowledge.Confidence, 3);
    }

    [Fact]
    public async Task ProcessAsync_completes_without_knowledge_when_extraction_is_empty()
    {
        var (db, sessionId) = await SeededPendingSessionAsync();
        var llm = new FakeLlmClient();

        var result = await Pipeline(db, llm).ProcessAsync(sessionId);

        Assert.Equal(PipelineOutcome.Completed, result.Outcome);
        Assert.Equal(0, result.KnowledgeCreated);
        Assert.Equal(0, await db.Knowledge.CountAsync());
    }

    [Fact]
    public async Task ProcessAsync_skips_knowledge_for_undomain_sessions_but_completes()
    {
        var (db, sessionId) = await SeededPendingSessionAsync(withDomain: false);
        var llm = new FakeLlmClient();
        llm.EnqueueExtractions(new KnowledgeExtraction(KnowledgeType.Solution, "X", LongContent, 0.8));

        var result = await Pipeline(db, llm).ProcessAsync(sessionId);

        Assert.Equal(PipelineOutcome.Completed, result.Outcome);
        Assert.Equal(0, result.KnowledgeCreated);
        Assert.Equal(0, await db.Knowledge.CountAsync());
        Assert.Equal(SessionStatus.Completed, (await db.Sessions.AsNoTracking().SingleAsync()).Status);
    }

    [Fact]
    public async Task ProcessAsync_returns_not_claimed_for_an_already_completed_session()
    {
        var (db, sessionId) = await SeededPendingSessionAsync();
        var llm = new FakeLlmClient();
        llm.EnqueueExtractions(new KnowledgeExtraction(KnowledgeType.Solution, "X", LongContent, 0.8));
        var first = await Pipeline(db, llm).ProcessAsync(sessionId);
        Assert.Equal(PipelineOutcome.Completed, first.Outcome);
        Assert.Equal(1, await db.Knowledge.CountAsync());

        var second = await Pipeline(db, llm).ProcessAsync(sessionId);

        Assert.Equal(PipelineOutcome.NotClaimed, second.Outcome);
        Assert.Equal(1, await db.Knowledge.CountAsync());
    }

    [Fact]
    public async Task ProcessNextAsync_is_idle_with_no_eligible_sessions()
    {
        var db = Fixture.CreateDbContext();
        await db.Database.MigrateAsync();

        var result = await Pipeline(db, new FakeLlmClient()).ProcessNextAsync();

        Assert.Equal(PipelineOutcome.Idle, result.Outcome);
        Assert.Null(result.SessionId);
    }

    [Fact]
    public async Task ProcessAsync_duplicate_content_corroborates_existing_knowledge_instead_of_duplicating()
    {
        // AC3: the same content reported by a second session bumps the existing
        // Knowledge's Confidence (capped) and creates no new row. The fake
        // embedding is content-hash-derived, so identical title+content yields
        // cosine distance ~0 — above the dedup threshold.
        var (db, firstSession, secondSession) = await SeedSharedSessionsAsync();
        var item = new KnowledgeExtraction(KnowledgeType.Solution, "Fix DB timeouts", LongContent, 0.8);
        var llm = new FakeLlmClient();
        llm.EnqueueExtractions(item);
        var first = await Pipeline(db, llm).ProcessAsync(firstSession);
        Assert.Equal(PipelineOutcome.Completed, first.Outcome);
        Assert.Equal(1, first.KnowledgeCreated);

        llm.EnqueueExtractions(item);
        var second = await Pipeline(db, llm).ProcessAsync(secondSession);

        Assert.Equal(PipelineOutcome.Completed, second.Outcome);
        Assert.Equal(0, second.KnowledgeCreated);
        Assert.Equal(1, second.KnowledgeCorroborated);

        Assert.Equal(1, await db.Knowledge.CountAsync());
        var knowledge = await db.Knowledge.AsNoTracking().SingleAsync();
        // 0.4×0.8 + 0.2×1.0 = 0.52, corroborated +0.05 → 0.57 (capped at 0.6)
        Assert.Equal(0.57, knowledge.Confidence, 3);
        // Corroboration keeps the original source session — the item was distilled from it.
        Assert.Equal(firstSession, knowledge.SourceSessionId);
    }

    [Fact]
    public async Task ProcessAsync_distinct_content_creates_new_knowledge()
    {
        var (db, firstSession, secondSession) = await SeedSharedSessionsAsync();
        var llm = new FakeLlmClient();
        llm.EnqueueExtractions(new KnowledgeExtraction(KnowledgeType.Solution, "Fix DB timeouts", LongContent, 0.8));
        await Pipeline(db, llm).ProcessAsync(firstSession);

        llm.EnqueueExtractions(new KnowledgeExtraction(KnowledgeType.Pattern, "Prefer immutable records", LongContent, 0.7));
        var result = await Pipeline(db, llm).ProcessAsync(secondSession);

        Assert.Equal(PipelineOutcome.Completed, result.Outcome);
        Assert.Equal(1, result.KnowledgeCreated);
        Assert.Equal(2, await db.Knowledge.CountAsync());
    }

    [Fact]
    public async Task ProcessAsync_corroboration_respects_the_max_confidence_cap()
    {
        var (db, firstSession, secondSession) = await SeedSharedSessionsAsync();
        var item = new KnowledgeExtraction(KnowledgeType.Pattern, "Perfect item", LongContent, 1.0);
        var llm = new FakeLlmClient();
        llm.EnqueueExtractions(item);
        await Pipeline(db, llm).ProcessAsync(firstSession); // confidence already 0.6

        llm.EnqueueExtractions(item);
        var result = await Pipeline(db, llm).ProcessAsync(secondSession);

        Assert.Equal(1, result.KnowledgeCorroborated);
        var knowledge = await db.Knowledge.AsNoTracking().SingleAsync();
        Assert.Equal(0.6, knowledge.Confidence, 3);
    }

    [Fact]
    public async Task ProcessAsync_extraction_failure_sets_retry_state()
    {
        // AC4: an extraction failure moves the Session to Failed with
        // NextAttemptAtUtc = now + backoff, ErrorCount incremented, LastError recorded.
        var (db, sessionId) = await SeededPendingSessionAsync();
        var llm = new FakeLlmClient();
        llm.FailNextExtractions(1);

        var result = await Pipeline(db, llm).ProcessAsync(sessionId);

        Assert.Equal(PipelineOutcome.Failed, result.Outcome);
        Assert.NotNull(result.Error);

        var session = await db.Sessions.AsNoTracking().SingleAsync();
        Assert.Equal(SessionStatus.Failed, session.Status);
        Assert.Equal(1, session.ErrorCount);
        Assert.False(string.IsNullOrEmpty(session.LastError));
        Assert.NotNull(session.NextAttemptAtUtc);
        Assert.True(session.NextAttemptAtUtc > DateTimeOffset.UtcNow.AddSeconds(20), "backoff should be roughly base (30 s)");
        Assert.Null(session.ProcessedAtUtc);
    }

    [Fact]
    public async Task ProcessAsync_retry_is_not_eligible_before_next_attempt_at()
    {
        var (db, sessionId) = await SeededPendingSessionAsync();
        var llm = new FakeLlmClient();
        llm.FailNextExtractions(1);
        await Pipeline(db, llm).ProcessAsync(sessionId);

        // Immediately retrying is refused — the backoff window has not elapsed.
        var early = await Pipeline(db, llm).ProcessAsync(sessionId);
        Assert.Equal(PipelineOutcome.NotClaimed, early.Outcome);

        // Time travel: once NextAttemptAtUtc has passed, the same Failed session
        // is claimed again and processed to completion (knowledge now created).
        await db.Sessions
            .Where(s => s.Id == sessionId)
            .ExecuteUpdateAsync(setters => setters.SetProperty(
                s => s.NextAttemptAtUtc, DateTimeOffset.UtcNow.AddSeconds(-1)));

        llm.EnqueueExtractions(new KnowledgeExtraction(KnowledgeType.Solution, "Fix DB timeouts", LongContent, 0.8));
        var retried = await Pipeline(db, llm).ProcessAsync(sessionId);

        Assert.Equal(PipelineOutcome.Completed, retried.Outcome);
        var session = await db.Sessions.AsNoTracking().SingleAsync(s => s.Id == sessionId);
        Assert.Equal(SessionStatus.Completed, session.Status);
        Assert.Equal(1, session.ErrorCount); // history preserved
        Assert.Null(session.LastError); // a successful run leaves no stale failure state
        Assert.Null(session.NextAttemptAtUtc);
        Assert.Equal(1, await db.Knowledge.CountAsync());
    }

    [Fact]
    public async Task ProcessAsync_exhausted_retry_budget_leaves_session_failed_and_visible()
    {
        // AC4: after the retry budget is exhausted the Session stays Failed
        // (queryable, never deleted) with no further automatic retry scheduled.
        var (db, sessionId) = await SeededPendingSessionAsync();
        var llm = new FakeLlmClient();
        llm.FailNextExtractions(LearningPipelineDefaults.MaxAttempts);

        for (var i = 0; i < LearningPipelineDefaults.MaxAttempts; i++)
        {
            await db.Sessions
                .Where(s => s.Id == sessionId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(
                    s => s.NextAttemptAtUtc, DateTimeOffset.UtcNow.AddSeconds(-1)));
            var failed = await Pipeline(db, llm).ProcessAsync(sessionId);
            Assert.Equal(PipelineOutcome.Failed, failed.Outcome);
        }

        var session = await db.Sessions.AsNoTracking().SingleAsync(s => s.Id == sessionId);
        Assert.Equal(SessionStatus.Failed, session.Status);
        Assert.Equal(LearningPipelineDefaults.MaxAttempts, session.ErrorCount);
        Assert.NotNull(session.LastError);
        Assert.Null(session.NextAttemptAtUtc); // no automatic retry scheduled

        // A further call refuses to claim it — but the row is still there and visible.
        var final = await Pipeline(db, llm).ProcessAsync(sessionId);
        Assert.Equal(PipelineOutcome.NotClaimed, final.Outcome);
        Assert.Equal(1, await db.Sessions.CountAsync(s => s.Id == sessionId));
    }

    [Fact]
    public async Task ProcessAsync_duplicate_items_within_one_response_create_single_knowledge()
    {
        // AC3 within a single extraction response: two identical items would both
        // miss the DB dedup (new rows are not persisted until the batch saves),
        // so they must be deduped in memory — one Knowledge row, one corroboration.
        var (db, sessionId) = await SeededPendingSessionAsync();
        var item = new KnowledgeExtraction(KnowledgeType.Solution, "Fix DB timeouts", LongContent, 0.8);
        var llm = new FakeLlmClient();
        llm.EnqueueExtractions(item, item);

        var result = await Pipeline(db, llm).ProcessAsync(sessionId);

        Assert.Equal(PipelineOutcome.Completed, result.Outcome);
        Assert.Equal(1, result.KnowledgeCreated);
        Assert.Equal(1, result.KnowledgeCorroborated);
        Assert.Equal(1, await db.Knowledge.CountAsync());
    }
}
