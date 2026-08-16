using AgentContext.Application.Dtos;

namespace AgentContext.Application.Contracts;

/// <summary>
/// Knowledge management (issue #6 / spec US 16 + 24): list with Confidence and
/// provenance, the sub-threshold review list, the per-item private marker,
/// deletion, and rate_knowledge — the dynamic Confidence feedback loop
/// (spec §6.3: citation +0.1 capped, not-useful clears).
/// </summary>
public interface IKnowledgeAppService
{
    /// <summary>All Active Knowledge, newest first (management UI list).</summary>
    Task<IReadOnlyList<KnowledgeListItem>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// The review list: Active Knowledge below the retrieval Confidence threshold
    /// (RetrievalDefaults.MinConfidence), with the threshold attached so the UI
    /// never hardcodes it.
    /// </summary>
    Task<ReviewKnowledgeResult> ListReviewAsync(CancellationToken cancellationToken = default);

    /// <summary>Toggles the per-item private marker (AC2).</summary>
    Task UpdateVisibilityAsync(Guid id, bool isPrivate, CancellationToken cancellationToken = default);

    /// <summary>Deletes the item; it is then gone from retrieval (AC4).</summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Restores an Archived item back to Active (T8 AC4), refreshing its usage
    /// timestamp so hygiene does not immediately re-archive it. Throws
    /// <see cref="KeyNotFoundException"/> when the id is not an archived item.
    /// </summary>
    Task RestoreAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Archived items (T8): restore or permanently remove them (AC4).</summary>
    Task<IReadOnlyList<KnowledgeListItem>> ListArchivedAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// rate_knowledge (AC3): useful → Confidence +0.1 (capped at 1.0);
    /// not useful → Confidence cleared to 0 and the item moves to Review
    /// (lands in the review list, T5/T8 semantics).
    /// </summary>
    Task<RateKnowledgeResult> RateAsync(Guid id, bool useful, CancellationToken cancellationToken = default);
}
