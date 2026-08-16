using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Application.Retrieval;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AgentContext.Application.Hygiene;

/// <inheritdoc cref="IKnowledgeHygieneAppService"/>
/// <summary>
/// Knowledge hygiene (T8 / spec US20 + §6.3 "temporal decay reduces long-unused
/// items"). Rules, per run:
/// <list type="bullet">
/// <item>Active items unused for a full decay window lose DecayStep Confidence
/// per window (capped at MaxDecay). When decay pushes Confidence below the
/// retrieval threshold the item moves to Review.</item>
/// <item>Review items untouched for ReviewGraceDays move to Archived.</item>
/// </list>
/// Idempotency (AC2): every rule only writes back items that cross a transition
/// boundary, so a re-run over unchanged data performs no writes.
/// </summary>
public sealed class KnowledgeHygieneAppService(AgentContextDbContext db) : IKnowledgeHygieneAppService
{
    public async Task<HygieneResult> RunOnceAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var decayCutoff = now.AddDays(-HygieneDefaults.DecayWindowDays);
        var archiveCutoff = now.AddDays(-HygieneDefaults.ReviewGraceDays);

        var items = await db.Knowledge
            .Where(k => k.Status == KnowledgeStatus.Active || k.Status == KnowledgeStatus.Review)
            .ToListAsync(cancellationToken);

        var decayed = 0;
        var movedToReview = 0;
        var archived = 0;

        foreach (var item in items)
        {
            // "Unused" = LastUsedAtUtc, falling back to the last write (creation or
            // previous decay). Decay advances UpdatedAtUtc, so a decayed-but-still-
            // Active item is not decayed again on the next run (idempotency, AC2).
            var lastUsed = item.LastUsedAtUtc ?? item.UpdatedAtUtc;

            if (item.Status == KnowledgeStatus.Active && lastUsed < decayCutoff)
            {
                var elapsedWindows = (int)((now - lastUsed).TotalDays / HygieneDefaults.DecayWindowDays);
                var decay = Math.Min(elapsedWindows * HygieneDefaults.DecayStep, HygieneDefaults.MaxDecay);
                if (decay <= 0)
                {
                    continue;
                }

                var next = Math.Max(0, item.Confidence - decay);
                if (next < RetrievalDefaults.MinConfidence)
                {
                    // Decayed below the retrieval threshold → Review.
                    item.Confidence = next;
                    item.Status = KnowledgeStatus.Review;
                    item.UpdatedAtUtc = now;
                    movedToReview++;
                    decayed++;
                }
                else
                {
                    // Still above threshold: decay in place, stays Active.
                    item.Confidence = next;
                    item.UpdatedAtUtc = now;
                    decayed++;
                }
            }
            else if (item.Status == KnowledgeStatus.Review && item.UpdatedAtUtc < archiveCutoff)
            {
                item.Status = KnowledgeStatus.Archived;
                item.UpdatedAtUtc = now;
                archived++;
            }
        }

        await db.SaveChangesAsync(cancellationToken);

        return new HygieneResult(decayed, movedToReview, archived);
    }
}
