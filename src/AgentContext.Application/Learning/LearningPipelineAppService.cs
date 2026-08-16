using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Application.Enums;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Pgvector;
using Pgvector.EntityFrameworkCore;

namespace AgentContext.Application.Learning;

/// <inheritdoc cref="ILearningPipelineAppService"/>
/// <summary>
/// The Learning Engine pipeline (issue #4, spec §6.3): claims a Session
/// atomically (Postgres-as-queue, ADR 0005), runs LLM extraction + embedding
/// through <see cref="ILlmClient"/>, computes the initial Confidence from
/// self-assessment + field completeness (first two weights, capped at 0.6),
/// dedups against same-domain Knowledge via cosine similarity, persists to
/// pgvector, and moves the Session to Completed — or to Failed with retry state
/// (ErrorCount / NextAttemptAtUtc / LastError). Permanent failures stay Failed
/// and visible; nothing is ever deleted.
/// </summary>
public sealed class LearningPipelineAppService(
    AgentContextDbContext db,
    ILlmClient llm,
    ISettingsAppService settings,
    ILogger<LearningPipelineAppService> logger) : ILearningPipelineAppService
{
    public async Task<LearningPipelineResult> ProcessNextAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var sessionId = await db.Sessions.AsNoTracking()
            .Where(IsEligible(null, now))
            .OrderBy(s => s.CreatedAtUtc)
            .Select(s => s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (sessionId == Guid.Empty)
        {
            return new LearningPipelineResult(null, PipelineOutcome.Idle);
        }

        return await ProcessAsync(sessionId, cancellationToken);
    }

    public async Task<LearningPipelineResult> ProcessAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        // The endpoint is stored in the database (spec: settings REST is a later
        // ticket, the seam isn't). Without it the pipeline idles and Sessions stay
        // Pending — never failing them for a configuration gap.
        if (await settings.GetLlmOptionsAsync(cancellationToken) is null)
        {
            logger.LogInformation(
                "LLM endpoint is not configured; session {SessionId} stays pending.", sessionId);
            return new LearningPipelineResult(null, PipelineOutcome.Idle);
        }

        var now = DateTimeOffset.UtcNow;

        // Atomic claim (Postgres-as-queue): Pending, or a Failed retry whose time
        // has come, becomes Processing. Concurrent workers can never claim twice.
        var claimed = await db.Sessions
            .Where(IsEligible(sessionId, now))
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Status, SessionStatus.Processing), cancellationToken);

        if (claimed == 0)
        {
            return new LearningPipelineResult(sessionId, PipelineOutcome.NotClaimed);
        }

        try
        {
            var session = await db.Sessions.AsNoTracking()
                .SingleAsync(s => s.Id == sessionId, cancellationToken);

            // Knowledge must land in an explicit Domain (CONTEXT.md). An
            // undomain'ed Session is completed without extraction — conservative
            // until a later ticket adds domain inference.
            if (session.DomainId is null)
            {
                logger.LogInformation("Session {SessionId} has no domain; skipping knowledge extraction.", sessionId);
                return await CompleteAsync(sessionId, now, 0, 0, cancellationToken);
            }

            var extractions = await llm.ExtractKnowledgeAsync(session.SummaryJson, cancellationToken);

            var created = 0;
            var corroborated = 0;
            // Rows created in this batch live only in the change tracker until the single
            // SaveChangesAsync below — so batch-internal duplicates must be deduped in memory
            // (against embeddings) before consulting the database, or they would all see "no
            // nearest neighbour" and each insert a copy (AC3 escape hatch).
            var batchEmbeddings = new List<float[]>();

            foreach (var item in extractions)
            {
                if (string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.Content))
                {
                    logger.LogWarning("Skipping extraction with missing title/content from session {SessionId}.", sessionId);
                    continue;
                }

                var embedding = await llm.EmbedAsync($"{item.Title}\n{item.Content}", cancellationToken);

                var duplicateInBatch = batchEmbeddings.Any(existing =>
                    CosineSimilarity(existing, embedding) >= LearningPipelineDefaults.DedupCosineThreshold);

                if (duplicateInBatch || await TryCorroborateAsync(session, embedding, now, cancellationToken))
                {
                    corroborated++;
                    continue;
                }

                batchEmbeddings.Add(embedding);
                db.Knowledge.Add(new Knowledge
                {
                    WorkspaceId = session.WorkspaceId,
                    DomainId = session.DomainId.Value,
                    Type = item.Type,
                    Title = item.Title,
                    Content = item.Content,
                    Confidence = ComputeInitialConfidence(item),
                    Embedding = new Vector(embedding),
                    SourceSessionId = session.Id,
                    Status = KnowledgeStatus.Active,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                });
                created++;
            }

            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Session {SessionId}: {Created} Knowledge created, {Corroborated} corroborated.",
                sessionId, created, corroborated);

            return await CompleteAsync(sessionId, now, created, corroborated, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Learning pipeline failed for session {SessionId}.", sessionId);
            return await FailAsync(sessionId, ex, cancellationToken);
        }
    }

    /// <summary>
    /// Dedup (AC3): if the new item's embedding is within the cosine threshold of
    /// an existing Active Knowledge in the same Domain, corroborate it — bump its
    /// Confidence (capped) and refresh UpdatedAtUtc — instead of creating a
    /// duplicate row. The bump is the T3 simplification (handoff TDD slice 4:
    /// "bump its corroboration/confidence"); the full 20% cross-session
    /// corroboration weight arrives with the T4/T5 retrieval-feedback hooks.
    /// The matching strategy is vector similarity; an LLM-judged alternative
    /// stays behind the ILlmClient abstraction for later.
    /// </summary>
    private async Task<bool> TryCorroborateAsync(
        Session session, float[] embedding, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var vector = new Vector(embedding);

        var nearest = await db.Knowledge.AsNoTracking()
            .Where(k => k.DomainId == session.DomainId
                && k.Status == KnowledgeStatus.Active
                && k.Embedding != null)
            .OrderBy(k => k.Embedding!.CosineDistance(vector))
            .Select(k => new { k.Id, Distance = k.Embedding!.CosineDistance(vector) })
            .FirstOrDefaultAsync(cancellationToken);

        if (nearest is null || 1 - nearest.Distance < LearningPipelineDefaults.DedupCosineThreshold)
        {
            return false;
        }

        var existing = await db.Knowledge.FindAsync([nearest.Id], cancellationToken);
        existing!.Confidence = Math.Min(
            LearningPipelineDefaults.MaxT3Confidence,
            existing.Confidence + LearningPipelineDefaults.CorroborationBump);
        existing.UpdatedAtUtc = now;

        logger.LogInformation(
            "Session {SessionId} corroborated existing Knowledge {KnowledgeId} (distance {Distance:F3}).",
            session.Id, existing.Id, nearest.Distance);

        return true;
    }

    private async Task<LearningPipelineResult> CompleteAsync(
        Guid sessionId, DateTimeOffset now, int created, int corroborated, CancellationToken cancellationToken)
    {
        await db.Sessions
            .Where(s => s.Id == sessionId && s.Status == SessionStatus.Processing)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Status, SessionStatus.Completed)
                .SetProperty(s => s.ProcessedAtUtc, now)
                // A successful (re)run leaves no stale failure state behind — the
                // health view must not show a Completed session as failed. ErrorCount
                // stays as audit history of how many attempts it took.
                .SetProperty(s => s.LastError, (string?)null)
                .SetProperty(s => s.NextAttemptAtUtc, (DateTimeOffset?)null), cancellationToken);

        return new LearningPipelineResult(sessionId, PipelineOutcome.Completed, created, corroborated);
    }

    /// <summary>
    /// Failure path (AC4): record the retry state — ErrorCount incremented,
    /// LastError captured, and NextAttemptAtUtc = now + exponential backoff
    /// (base 30 s, ×2, capped at 1 h). Once the retry budget is exhausted the
    /// Session stays Failed with NextAttemptAtUtc = null: visible, queryable,
    /// never lost, never deleted.
    /// </summary>
    private async Task<LearningPipelineResult> FailAsync(Guid sessionId, Exception ex, CancellationToken cancellationToken)
    {
        var attempt = await db.Sessions.AsNoTracking()
            .Where(s => s.Id == sessionId)
            .Select(s => s.ErrorCount + 1)
            .SingleAsync(cancellationToken);

        var backoff = TimeSpan.FromSeconds(Math.Min(
            LearningPipelineDefaults.RetryMaxDelay.TotalSeconds,
            LearningPipelineDefaults.RetryBaseDelay.TotalSeconds * Math.Pow(2, attempt - 1)));

        var retriesExhausted = attempt >= LearningPipelineDefaults.MaxAttempts;
        var nextAttemptAt = retriesExhausted ? (DateTimeOffset?)null : DateTimeOffset.UtcNow + backoff;

        await db.Sessions
            .Where(s => s.Id == sessionId && s.Status == SessionStatus.Processing)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(s => s.Status, SessionStatus.Failed)
                .SetProperty(s => s.ErrorCount, attempt)
                .SetProperty(s => s.LastError, ex.Message)
                .SetProperty(s => s.NextAttemptAtUtc, nextAttemptAt), cancellationToken);

        return new LearningPipelineResult(sessionId, PipelineOutcome.Failed, Error: ex.Message);
    }

    /// <summary>
    /// Initial Confidence for T3 = self-assessment weight × assessment + field
    /// completeness weight × completeness, capped at 0.6 (the two implemented
    /// weights). Cross-session corroboration and novelty weights arrive with the
    /// T4/T5 retrieval-feedback hooks; dynamic adjustments are out of scope.
    /// </summary>
    private static double ComputeInitialConfidence(KnowledgeExtraction item)
    {
        var selfAssessment = Math.Clamp(item.SelfAssessment, 0, 1);
        var completeness = FieldCompletenessScore(item);

        return Math.Clamp(
            LearningPipelineDefaults.SelfAssessmentWeight * selfAssessment
            + LearningPipelineDefaults.CompletenessWeight * completeness,
            0, LearningPipelineDefaults.MaxT3Confidence);
    }

    /// <summary>Field completeness (0..1): title present, content present, content substantial.</summary>
    private static double FieldCompletenessScore(KnowledgeExtraction item)
    {
        var score = 0.0;
        if (!string.IsNullOrWhiteSpace(item.Title)) score += 0.4;
        if (!string.IsNullOrWhiteSpace(item.Content)) score += 0.4;
        if (item.Content.Length >= 80) score += 0.2;
        return Math.Clamp(score, 0, 1);
    }

    /// <summary>
    /// Eligibility for claiming, shared by the worker poll (sessionId null) and the
    /// per-session seam: Pending, or a Failed retry whose NextAttemptAtUtc has arrived.
    /// </summary>
    private static System.Linq.Expressions.Expression<Func<Session, bool>> IsEligible(Guid? sessionId, DateTimeOffset now)
        => s => (sessionId == null || s.Id == sessionId)
            && (s.Status == SessionStatus.Pending
                || (s.Status == SessionStatus.Failed && s.NextAttemptAtUtc <= now));

    /// <summary>Cosine similarity of two equal-length vectors (1 = identical).</summary>
    private static double CosineSimilarity(float[] a, float[] b)
    {
        double dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        return normA == 0 || normB == 0 ? 0 : dot / (Math.Sqrt(normA) * Math.Sqrt(normB));
    }
}
