using System.Diagnostics;
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
    ILogger<LearningPipelineAppService> logger,
    IInferenceConfigurationAppService? inference = null) : ILearningPipelineAppService
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
        // Without a complete inference configuration the pipeline idles and
        // Sessions stay Pending — never failing them for a configuration gap.
        var configured = inference is null
            ? await settings.GetLlmOptionsAsync(cancellationToken) is not null
            : await inference.GetRuntimeOptionsAsync(cancellationToken) is not null;
        if (!configured)
        {
            logger.LogInformation(
                "Inference routes are not configured; session {SessionId} stays pending.", sessionId);
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

        // T13 (issue #14): one span per pipeline run so the work shows up as traces
        // in the Aspire dashboard (the source is subscribed by the host's trace provider).
        using var activity = LearningPipelineTelemetry.Source.StartActivity("learning-pipeline.process");
        activity?.SetTag("session.id", sessionId);

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
                var skipped = await CompleteAsync(sessionId, now, 0, 0, cancellationToken);
                activity?.SetTag("pipeline.outcome", skipped.Outcome.ToString());
                activity?.SetStatus(ActivityStatusCode.Ok);
                return skipped;
            }

            var extractions = await llm.ExtractKnowledgeAsync(session.SummaryJson, cancellationToken);

            var created = 0;
            var corroborated = 0;
            // Rows created in this batch live only in the change tracker until the single
            // SaveChangesAsync below — so batch-internal matches must be evaluated in memory
            // (against embeddings) too, or they would all see "no nearest neighbour" in the
            // database and each insert a copy (AC3 escape hatch).
            var batchItems = new List<(float[] Embedding, Knowledge Knowledge)>();

            foreach (var item in extractions)
            {
                if (string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.Content))
                {
                    logger.LogWarning("Skipping extraction with missing title/content from session {SessionId}.", sessionId);
                    continue;
                }

                var embedding = await llm.EmbedAsync($"{item.Title}\n{item.Content}", cancellationToken);
                var dbNearest = await FindNearestAsync(session, embedding, cancellationToken);
                var batchNearest = FindRelatedInBatch(batchItems, embedding);

                // Dedup (AC3): identical content corroborates the existing row — no new row.
                if (dbNearest is { Sim: >= LearningPipelineDefaults.DedupCosineThreshold } dbDuplicate)
                {
                    Corroborate(dbDuplicate.Knowledge, now);
                    // Register it so further identical items in this batch don't double-bump.
                    batchItems.Add((embedding, dbDuplicate.Knowledge));
                    corroborated++;
                    continue;
                }

                if (batchNearest is { Sim: >= LearningPipelineDefaults.DedupCosineThreshold })
                {
                    // Duplicate WITHIN this session's extraction — counted, but not a
                    // cross-session corroboration, so no Confidence bump.
                    corroborated++;
                    continue;
                }

                // Conflict (T4): related-but-distinct content is kept as a real row and
                // paired via a shared ConflictGroupId so retrieval can show both sides.
                var mostSimilar = PickMostSimilar(dbNearest, batchNearest);
                string? conflictGroupId = null;
                if (mostSimilar is { Sim: >= LearningPipelineDefaults.ConflictMinSimilarity } conflict)
                {
                    conflictGroupId = conflict.Knowledge.ConflictGroupId;
                    if (conflictGroupId is null)
                    {
                        conflictGroupId = Guid.CreateVersion7().ToString();
                        conflict.Knowledge.ConflictGroupId = conflictGroupId;
                    }
                }

                var knowledge = new Knowledge
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
                    ConflictGroupId = conflictGroupId,
                    CreatedAtUtc = now,
                    UpdatedAtUtc = now,
                };
                db.Knowledge.Add(knowledge);
                batchItems.Add((embedding, knowledge));
                created++;
            }

            await db.SaveChangesAsync(cancellationToken);

            logger.LogInformation(
                "Session {SessionId}: {Created} Knowledge created, {Corroborated} corroborated.",
                sessionId, created, corroborated);

            var result = await CompleteAsync(sessionId, now, created, corroborated, cancellationToken);
            activity?.SetTag("knowledge.created", created);
            activity?.SetTag("knowledge.corroborated", corroborated);
            activity?.SetTag("pipeline.outcome", result.Outcome.ToString());
            activity?.SetStatus(ActivityStatusCode.Ok);
            return result;
        }
        catch (Exception ex)
        {
            activity?.SetTag("pipeline.outcome", PipelineOutcome.Failed.ToString());
            activity?.SetTag("error.type", ex.GetType().Name);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
            logger.LogError(ex, "Learning pipeline failed for session {SessionId}.", sessionId);
            return await FailAsync(sessionId, ex, cancellationToken);
        }
    }

    /// <summary>
    /// Dedup (AC3): identical content corroborates the existing Knowledge — bump its
    /// Confidence (capped) and refresh UpdatedAtUtc — instead of creating a duplicate
    /// row. The bump is the T3 simplification (handoff TDD slice 4); the full 20%
    /// cross-session corroboration weight arrives with the T5 retrieval-feedback hooks.
    /// The matching strategy is vector similarity; an LLM-judged alternative stays
    /// behind the ILlmClient abstraction for later.
    /// </summary>
    private void Corroborate(Knowledge existing, DateTimeOffset now)
    {
        existing.Confidence = Math.Min(
            LearningPipelineDefaults.MaxT3Confidence,
            existing.Confidence + LearningPipelineDefaults.CorroborationBump);
        existing.UpdatedAtUtc = now;

        logger.LogInformation("Corroborated existing Knowledge {KnowledgeId}.", existing.Id);
    }

    /// <summary>
    /// Nearest Active Knowledge in the same Domain, tracked (not AsNoTracking) so
    /// callers can mutate it — bump confidence or attach a ConflictGroupId.
    /// </summary>
    private async Task<Candidate?> FindNearestAsync(
        Session session, float[] embedding, CancellationToken cancellationToken)
    {
        var vector = new Vector(embedding);

        var nearest = await db.Knowledge
            .Where(k => k.DomainId == session.DomainId
                && k.Status == KnowledgeStatus.Active
                && k.Embedding != null)
            .OrderBy(k => k.Embedding!.CosineDistance(vector))
            .Select(k => new { k, Distance = k.Embedding!.CosineDistance(vector) })
            .FirstOrDefaultAsync(cancellationToken);

        return nearest is null ? null : new Candidate(1 - nearest.Distance, nearest.k);
    }

    /// <summary>
    /// Most similar item created earlier in this batch (change-tracker rows, not yet
    /// saved); only candidates at or above the conflict band are considered — below
    /// that, the item is unrelated to anything in the batch.
    /// </summary>
    private static Candidate? FindRelatedInBatch(List<(float[] Embedding, Knowledge Knowledge)> batch, float[] embedding)
    {
        Candidate? best = null;
        foreach (var (existing, knowledge) in batch)
        {
            var similarity = CosineSimilarity(existing, embedding);
            if (similarity >= LearningPipelineDefaults.ConflictMinSimilarity
                && (best is null || similarity > best.Sim))
            {
                best = new Candidate(similarity, knowledge);
            }
        }

        return best;
    }

    private static Candidate? PickMostSimilar(Candidate? a, Candidate? b)
        => a is null ? b : b is null ? a : a.Sim >= b.Sim ? a : b;

    private sealed record Candidate(double Sim, Knowledge Knowledge);

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
