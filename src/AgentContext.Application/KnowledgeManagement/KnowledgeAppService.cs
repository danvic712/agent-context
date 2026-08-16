using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Application.Learning;
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
        var items = await ActiveItems()
            .Where(k => k.Confidence < RetrievalDefaults.MinConfidence)
            .OrderBy(k => k.Confidence)
            .Select(ToItem)
            .ToListAsync(cancellationToken);

        return new ReviewKnowledgeResult(RetrievalDefaults.MinConfidence, items);
    }

    public async Task UpdateVisibilityAsync(Guid id, bool isPrivate, CancellationToken cancellationToken = default)
    {
        var updated = await db.Knowledge
            .Where(k => k.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(k => k.IsPrivate, isPrivate)
                .SetProperty(k => k.UpdatedAtUtc, DateTimeOffset.UtcNow), cancellationToken);

        if (updated == 0)
        {
            throw new KeyNotFoundException($"Knowledge {id} not found.");
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Physical delete (AC4): the row is gone, so retrieval can never return it.
        // Soft-delete/archive semantics belong to the T8 decay/hygiene work.
        var deleted = await db.Knowledge.Where(k => k.Id == id).ExecuteDeleteAsync(cancellationToken);

        if (deleted == 0)
        {
            throw new KeyNotFoundException($"Knowledge {id} not found.");
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
            throw new KeyNotFoundException($"Knowledge {id} not found.");
        }

        // T5 ticket: rate_knowledge(useful) is the "citation confirms" signal —
        // +0.1 capped at 1.0; not-useful clears to 0 (the item then falls below
        // the retrieval threshold → review list). Full citation tracking lands
        // with retrieval-feedback work; temporal decay is T8.
        var next = useful
            ? Math.Min(LearningPipelineDefaults.MaxConfidence,
                current.Value + LearningPipelineDefaults.RateConfidenceBump)
            : 0;

        await db.Knowledge
            .Where(k => k.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(k => k.Confidence, next)
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
