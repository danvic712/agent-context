using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using DomainEntity = AgentContext.Domain.Entities.Domain;

namespace AgentContext.Application.Retrieval;

/// <inheritdoc cref="IRetrievalAppService"/>
/// <summary>
/// Shared retrieval backend (issue #5 / spec US 17/18/19): embeds the query
/// through <see cref="ILlmClient"/> and ranks Active Knowledge in the requested
/// Domain by pgvector cosine distance, filtering Confidence ≥ threshold (default
/// 0.5). Conflict-group partners are appended so both sides show side by side.
/// Single-user MVP: private items are always visible to the owner (AC5).
/// </summary>
public sealed class RetrievalAppService(AgentContextDbContext db, ILlmClient llm) : IRetrievalAppService
{
    public async Task<SearchMemoryResult> SearchMemoryAsync(
        string domain, string query, double? minConfidence = null, CancellationToken cancellationToken = default)
    {
        var threshold = minConfidence ?? RetrievalDefaults.MinConfidence;
        var domainId = await ResolveDomainIdAsync(domain, cancellationToken);
        if (domainId is null)
        {
            return new SearchMemoryResult([]);
        }

        var items = await RankedMatchesAsync(domainId.Value, query, threshold, RetrievalDefaults.TopN, cancellationToken);
        var grouped = await AppendConflictPartnersAsync(domainId.Value, items, cancellationToken);

        return new SearchMemoryResult(grouped);
    }

    public async Task<FindSimilarSolutionResult> FindSimilarSolutionAsync(
        string domain, string problem, CancellationToken cancellationToken = default)
    {
        var domainId = await ResolveDomainIdAsync(domain, cancellationToken);
        if (domainId is null)
        {
            return new FindSimilarSolutionResult(null, []);
        }

        var matches = await RankedMatchesAsync(
            domainId.Value, problem, RetrievalDefaults.MinConfidence, 1,
            cancellationToken, type: KnowledgeType.Solution);

        var solution = matches.FirstOrDefault();
        if (solution is null)
        {
            return new FindSimilarSolutionResult(null, []);
        }

        // Conflict partners are answer-level disagreements: same group, same
        // KnowledgeType (Solution) — a Problem row in the group is not a conflict.
        // Domain-scoped like the main query (AC1).
        var conflicts = await db.Knowledge.AsNoTracking()
            .Where(k => k.DomainId == domainId
                && k.ConflictGroupId == solution.ConflictGroupId
                && k.Id != solution.Id
                && k.Status == KnowledgeStatus.Active
                && k.Type == KnowledgeType.Solution)
            .ToListAsync(cancellationToken);

        return new FindSimilarSolutionResult(solution, conflicts.Select(c => ToItem(c, score: 0)).ToList());
    }

    /// <summary>Top-N Active Knowledge in the domain ranked by cosine distance to the query.</summary>
    private async Task<List<KnowledgeSearchItem>> RankedMatchesAsync(
        Guid domainId, string query, double threshold, int take,
        CancellationToken cancellationToken, KnowledgeType? type = null)
    {
        var vector = new Vector(await llm.EmbedAsync(query, cancellationToken));

        var queryable = db.Knowledge.AsNoTracking()
            .Where(k => k.DomainId == domainId
                && k.Status == KnowledgeStatus.Active
                && k.Embedding != null
                && k.Confidence >= threshold);

        if (type is not null)
        {
            queryable = queryable.Where(k => k.Type == type);
        }

        var matches = await queryable
            .OrderBy(k => k.Embedding!.CosineDistance(vector))
            .Select(k => new { k, Distance = k.Embedding!.CosineDistance(vector) })
            .Take(take)
            .ToListAsync(cancellationToken);

        return matches.Select(m => ToItem(m.k, score: Math.Clamp(1 - m.Distance, 0, 1))).ToList();
    }

    /// <summary>
    /// Appends every conflict-group partner of the ranked items that is not
    /// already present (AC4/US19: both sides of a contradiction are visible for
    /// judgement). Partners deliberately bypass the Confidence threshold — seeing
    /// the low-confidence side of a conflict IS the point — but are marked with
    /// Score = 0 (present, not ranked) so consumers can tell them apart.
    /// </summary>
    private async Task<List<KnowledgeSearchItem>> AppendConflictPartnersAsync(
        Guid domainId, List<KnowledgeSearchItem> items, CancellationToken cancellationToken)
    {
        var groupIds = items.Where(i => i.ConflictGroupId is not null).Select(i => i.ConflictGroupId!).Distinct().ToList();
        if (groupIds.Count == 0)
        {
            return items;
        }

        var itemIds = items.Select(i => i.Id).ToList();
        var partners = await db.Knowledge.AsNoTracking()
            .Where(k => k.DomainId == domainId
                && k.Status == KnowledgeStatus.Active
                && groupIds.Contains(k.ConflictGroupId!)
                && !itemIds.Contains(k.Id))
            .ToListAsync(cancellationToken);

        var result = new List<KnowledgeSearchItem>(items);
        result.AddRange(partners.Select(p => ToItem(p, score: 0)));
        return result;
    }

    /// <summary>
    /// Resolves a Domain by name inside the first Workspace — the single-user MVP
    /// has one workspace, consistent with save_session.
    /// </summary>
    private async Task<Guid?> ResolveDomainIdAsync(string domainName, CancellationToken cancellationToken)
    {
        var workspaceId = await db.Workspaces.AsNoTracking()
            .OrderBy(w => w.CreatedAtUtc)
            .Select(w => w.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (workspaceId == Guid.Empty)
        {
            return null;
        }

        return await db.Domains.AsNoTracking()
            .Where(d => d.WorkspaceId == workspaceId && d.Name == domainName)
            .Select(d => (Guid?)d.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private static KnowledgeSearchItem ToItem(Knowledge k, double score) => new(
        k.Id, k.Type, k.Title, k.Content, k.Confidence, k.ConflictGroupId, k.IsPrivate, score);
}
