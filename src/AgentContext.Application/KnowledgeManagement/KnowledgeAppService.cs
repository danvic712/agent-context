using System.Net;
using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Application.Learning;
using AgentContext.Application.Localization;
using AgentContext.Application.Retrieval;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AgentContext.Application.KnowledgeManagement;

/// <inheritdoc cref="IKnowledgeAppService"/>
/// <summary>
/// Knowledge management (issue #6): the management list, the sub-threshold
/// review list, the per-item private marker, physical deletion, and
/// rate_knowledge — the dynamic Confidence feedback loop (spec §6.3).
/// </summary>
public sealed class KnowledgeAppService(AgentContextDbContext db) : IKnowledgeAppService
{
    public async Task<IReadOnlyList<KnowledgeListItem>> ListAsync(CancellationToken cancellationToken = default)
        => await ActiveItems()
            .OrderByDescending(k => k.CreatedAtUtc)
            .Select(ToItem)
            .ToListAsync(cancellationToken);

    public async Task<ReviewKnowledgeResult> ListReviewAsync(CancellationToken cancellationToken = default)
    {
        // T8: Review is an explicit status (hygiene moves decayed items here,
        // rate_knowledge(not-useful) clears into it). The threshold is attached
        // for the UI's label — the item set comes from the status, not a query.
        var items = await db.Knowledge.AsNoTracking()
            .Where(k => k.Status == KnowledgeStatus.Review)
            .OrderBy(k => k.Confidence)
            .Select(ToItem)
            .ToListAsync(cancellationToken);

        return new ReviewKnowledgeResult(RetrievalDefaults.MinConfidence, items);
    }

    public async Task<IReadOnlyList<KnowledgeListItem>> ListArchivedAsync(CancellationToken cancellationToken = default)
        => await db.Knowledge.AsNoTracking()
            .Where(k => k.Status == KnowledgeStatus.Archived)
            .OrderByDescending(k => k.UpdatedAtUtc)
            .Select(ToItem)
            .ToListAsync(cancellationToken);

    public async Task UpdateVisibilityAsync(Guid id, bool isPrivate, CancellationToken cancellationToken = default)
    {
        var updated = await db.Knowledge
            .Where(k => k.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(k => k.IsPrivate, isPrivate)
                .SetProperty(k => k.UpdatedAtUtc, DateTimeOffset.UtcNow), cancellationToken);

        if (updated == 0)
        {
            throw new LocalizedException(HttpStatusCode.NotFound, ErrorCodes.Knowledge.NotFound, id);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Physical delete (AC4): the row is gone, so retrieval can never return it.
        // Soft-delete/archive semantics belong to the T8 decay/hygiene work.
        var deleted = await db.Knowledge.Where(k => k.Id == id).ExecuteDeleteAsync(cancellationToken);

        if (deleted == 0)
        {
            throw new LocalizedException(HttpStatusCode.NotFound, ErrorCodes.Knowledge.NotFound, id);
        }
    }

    public async Task RestoreAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // AC4 (T8): a user can restore an archived item back to Active. The
        // restored item is treated as freshly used so decay does not re-archive it.
        var updated = await db.Knowledge
            .Where(k => k.Id == id && k.Status == KnowledgeStatus.Archived)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(k => k.Status, KnowledgeStatus.Active)
                .SetProperty(k => k.LastUsedAtUtc, DateTimeOffset.UtcNow)
                .SetProperty(k => k.UpdatedAtUtc, DateTimeOffset.UtcNow), cancellationToken);

        if (updated == 0)
        {
            throw new LocalizedException(HttpStatusCode.NotFound, ErrorCodes.Knowledge.NotArchived, id);
        }
    }

    public async Task<RateKnowledgeResult> RateAsync(Guid id, bool useful, CancellationToken cancellationToken = default)
    {
        var current = await db.Knowledge.AsNoTracking()
            .Where(k => k.Id == id)
            .Select(k => (double?)k.Confidence)
            .FirstOrDefaultAsync(cancellationToken);

        if (current is null)
        {
            throw new LocalizedException(HttpStatusCode.NotFound, ErrorCodes.Knowledge.NotFound, id);
        }

        // rate_knowledge(useful) confirms: +0.1 capped at 1.0, and the item is
        // pulled back to Active (it was confirmed useful). not-useful clears to 0
        // and moves the item into Review (T8: Review is an explicit status).
        var next = useful
            ? Math.Min(LearningPipelineDefaults.MaxConfidence,
                current.Value + LearningPipelineDefaults.RateConfidenceBump)
            : 0;
        var nextStatus = useful ? KnowledgeStatus.Active : KnowledgeStatus.Review;

        await db.Knowledge
            .Where(k => k.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(k => k.Confidence, next)
                .SetProperty(k => k.Status, nextStatus)
                .SetProperty(k => k.UpdatedAtUtc, DateTimeOffset.UtcNow), cancellationToken);

        return new RateKnowledgeResult(id, next);
    }

    private IQueryable<Knowledge> ActiveItems()
        => db.Knowledge.AsNoTracking().Where(k => k.Status == KnowledgeStatus.Active);

    private static readonly System.Linq.Expressions.Expression<Func<Knowledge, KnowledgeListItem>> ToItem =
        k => new KnowledgeListItem(
            k.Id,
            k.Type,
            k.Title,
            k.Content,
            k.Confidence,
            k.IsPrivate,
            k.Domain != null ? k.Domain.Name : null,
            k.SourceSession != null ? k.SourceSession.Task : null,
            k.CreatedAtUtc,
            k.UpdatedAtUtc);
}
