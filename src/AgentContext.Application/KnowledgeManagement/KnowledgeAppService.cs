using System.Net;
using System.Text.Json;
using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Application.Localization;
using AgentContext.Application.Learning;
using AgentContext.Application.Retrieval;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AgentContext.Application.KnowledgeManagement;

/// <inheritdoc cref="IKnowledgeAppService"/>
/// <summary>
/// Knowledge management for the unified library surface. List traversal uses
/// keyset pagination so a growing library does not shift rows between pages.
/// </summary>
public sealed class KnowledgeAppService(AgentContextDbContext db) : IKnowledgeAppService
{
    private const int DefaultPageSize = 30;
    private const int MaxPageSize = 100;
    private const string CursorVersion = "knowledge-library-v1";

    public async Task<KnowledgeLibraryResult> ListLibraryAsync(
        KnowledgeLibraryQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var limit = query.Limit ?? DefaultPageSize;
        if (limit is < 1 or > MaxPageSize)
        {
            throw new LocalizedException(
                HttpStatusCode.BadRequest,
                ErrorCodes.Knowledge.PageSizeInvalid,
                DefaultPageSize,
                MaxPageSize);
        }

        var status = query.Status ?? KnowledgeStatus.Active;
        var search = NormalizeSearch(query.Search);
        var cursor = DecodeCursor(query.Cursor);
        ValidateCursor(cursor, status, search);

        var source = db.Knowledge.AsNoTracking()
            .Where(k => k.Status == status);

        if (search is not null)
        {
            source = source.Where(k =>
                k.Title.ToLower().Contains(search)
                || k.Content.ToLower().Contains(search)
                || (k.Domain != null && k.Domain.Name.ToLower().Contains(search))
                || (k.SourceSession != null && k.SourceSession.Task.ToLower().Contains(search)));
        }

        if (cursor is not null)
        {
            source = ApplyCursor(source, cursor);
        }

        var rows = await ApplySort(source, status)
            .Select(ToItem)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > limit;
        var items = rows.Take(limit).ToList();
        var nextCursor = hasMore && items.Count > 0
            ? EncodeCursor(items[^1], status, search)
            : null;
        var counts = await LoadCountsAsync(cancellationToken);

        return new KnowledgeLibraryResult(
            limit,
            query.Cursor,
            items,
            hasMore,
            nextCursor,
            counts,
            RetrievalDefaults.MinConfidence);
    }

    public async Task UpdateVisibilityAsync(Guid id, bool isPrivate, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var updated = await db.Knowledge
            .Where(k => k.Id == id)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(k => k.IsPrivate, isPrivate)
                .SetProperty(k => k.UpdatedAtUtc, now), cancellationToken);

        if (updated == 0)
        {
            throw new LocalizedException(HttpStatusCode.NotFound, ErrorCodes.Knowledge.NotFound, id);
        }
    }

    public async Task SendToReviewAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var updated = await db.Knowledge
            .Where(k => k.Id == id && k.Status == KnowledgeStatus.Active)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(k => k.Status, KnowledgeStatus.Review)
                .SetProperty(k => k.UpdatedAtUtc, now), cancellationToken);

        if (updated == 0)
        {
            throw new LocalizedException(HttpStatusCode.NotFound, ErrorCodes.Knowledge.NotActive, id);
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var deleted = await db.Knowledge
            .Where(k => k.Id == id)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted == 0)
        {
            throw new LocalizedException(HttpStatusCode.NotFound, ErrorCodes.Knowledge.NotFound, id);
        }
    }

    public async Task RestoreAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;
        var updated = await db.Knowledge
            .Where(k => k.Id == id && k.Status == KnowledgeStatus.Archived)
            .ExecuteUpdateAsync(setters => setters
                .SetProperty(k => k.Status, KnowledgeStatus.Active)
                .SetProperty(k => k.LastUsedAtUtc, now)
                .SetProperty(k => k.LastConfidenceDecayAtUtc, now)
                .SetProperty(k => k.UpdatedAtUtc, now), cancellationToken);

        if (updated == 0)
        {
            throw new LocalizedException(HttpStatusCode.NotFound, ErrorCodes.Knowledge.NotArchived, id);
        }
    }

    public async Task<RateKnowledgeResult> RateAsync(Guid id, bool useful, CancellationToken cancellationToken = default)
    {
        var current = await db.Knowledge.AsNoTracking()
            .Where(k => k.Id == id)
            .Select(k => new { k.Confidence, k.Status })
            .FirstOrDefaultAsync(cancellationToken);

        if (current is null)
        {
            throw new LocalizedException(HttpStatusCode.NotFound, ErrorCodes.Knowledge.NotFound, id);
        }

        if (current.Status == KnowledgeStatus.Archived)
        {
            throw new LocalizedException(HttpStatusCode.Conflict, ErrorCodes.Knowledge.ArchivedCannotRate, id);
        }

        var next = useful
            ? Math.Min(
                LearningPipelineDefaults.MaxConfidence,
                current.Confidence + LearningPipelineDefaults.RateConfidenceBump)
            : 0;
        var nextStatus = useful ? KnowledgeStatus.Active : KnowledgeStatus.Review;
        var now = DateTimeOffset.UtcNow;

        if (useful)
        {
            await db.Knowledge
                .Where(k => k.Id == id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(k => k.Confidence, next)
                    .SetProperty(k => k.Status, nextStatus)
                    .SetProperty(k => k.LastUsedAtUtc, now)
                    .SetProperty(k => k.LastConfidenceDecayAtUtc, now)
                    .SetProperty(k => k.UpdatedAtUtc, now), cancellationToken);
        }
        else
        {
            await db.Knowledge
                .Where(k => k.Id == id)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(k => k.Confidence, next)
                    .SetProperty(k => k.Status, nextStatus)
                    .SetProperty(k => k.UpdatedAtUtc, now), cancellationToken);
        }

        return new RateKnowledgeResult(id, next);
    }

    private async Task<KnowledgeLibraryCounts> LoadCountsAsync(CancellationToken cancellationToken)
    {
        var counts = await db.Knowledge.AsNoTracking()
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Active = group.Count(k => k.Status == KnowledgeStatus.Active),
                Review = group.Count(k => k.Status == KnowledgeStatus.Review),
                Archived = group.Count(k => k.Status == KnowledgeStatus.Archived),
            })
            .FirstOrDefaultAsync(cancellationToken);

        return counts is null
            ? new KnowledgeLibraryCounts(0, 0, 0)
            : new KnowledgeLibraryCounts(counts.Active, counts.Review, counts.Archived);
    }

    private static string? NormalizeSearch(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static IQueryable<Knowledge> ApplyCursor(
        IQueryable<Knowledge> source,
        KnowledgeLibraryCursor cursor)
        => cursor.Status switch
        {
            KnowledgeStatus.Review => source.Where(k =>
                k.Confidence > cursor.Confidence
                || (k.Confidence == cursor.Confidence
                    && (k.UpdatedAtUtc > cursor.UpdatedAtUtc
                        || (k.UpdatedAtUtc == cursor.UpdatedAtUtc
                            && k.Id.CompareTo(cursor.Id) > 0)))),
            KnowledgeStatus.Archived => source.Where(k =>
                k.UpdatedAtUtc < cursor.UpdatedAtUtc
                || (k.UpdatedAtUtc == cursor.UpdatedAtUtc
                    && k.Id.CompareTo(cursor.Id) < 0)),
            _ => source.Where(k =>
                k.CreatedAtUtc < cursor.CreatedAtUtc
                || (k.CreatedAtUtc == cursor.CreatedAtUtc
                    && k.Id.CompareTo(cursor.Id) < 0)),
        };

    private static IQueryable<Knowledge> ApplySort(
        IQueryable<Knowledge> source,
        KnowledgeStatus status)
        => status switch
        {
            KnowledgeStatus.Review => source
                .OrderBy(k => k.Confidence)
                .ThenBy(k => k.UpdatedAtUtc)
                .ThenBy(k => k.Id),
            KnowledgeStatus.Archived => source
                .OrderByDescending(k => k.UpdatedAtUtc)
                .ThenByDescending(k => k.Id),
            _ => source
                .OrderByDescending(k => k.CreatedAtUtc)
                .ThenByDescending(k => k.Id),
        };

    private static void ValidateCursor(
        KnowledgeLibraryCursor? cursor,
        KnowledgeStatus status,
        string? search)
    {
        if (cursor is null)
        {
            return;
        }

        if (!string.Equals(cursor.Version, CursorVersion, StringComparison.Ordinal)
            || cursor.Status != status
            || !string.Equals(cursor.Search, search, StringComparison.Ordinal)
            || cursor.Id == Guid.Empty)
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Knowledge.CursorInvalid);
        }

        if (status == KnowledgeStatus.Review && cursor.Confidence is null)
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Knowledge.CursorInvalid);
        }

        if (status != KnowledgeStatus.Review && cursor.CreatedAtUtc == default && cursor.UpdatedAtUtc == default)
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Knowledge.CursorInvalid);
        }
    }

    private static KnowledgeLibraryCursor? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            var base64 = cursor.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            var decoded = JsonSerializer.Deserialize<KnowledgeLibraryCursor>(Convert.FromBase64String(base64));
            if (decoded is null
                || decoded.Version is null
                || decoded.Status is < KnowledgeStatus.Active or > KnowledgeStatus.Archived)
            {
                throw new JsonException("The cursor payload is incomplete.");
            }

            return decoded;
        }
        catch (FormatException)
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Knowledge.CursorInvalid);
        }
        catch (JsonException)
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Knowledge.CursorInvalid);
        }
    }

    private static string EncodeCursor(
        KnowledgeListItem item,
        KnowledgeStatus status,
        string? search)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new KnowledgeLibraryCursor(
            CursorVersion,
            status,
            search,
            item.Confidence,
            item.CreatedAtUtc,
            item.UpdatedAtUtc,
            item.Id));
        return Convert.ToBase64String(payload)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static readonly System.Linq.Expressions.Expression<Func<Knowledge, KnowledgeListItem>> ToItem =
        k => new KnowledgeListItem(
            k.Id,
            k.Status,
            k.Type,
            k.Title,
            k.Content,
            k.Confidence,
            k.IsPrivate,
            k.Domain != null ? k.Domain.Name : null,
            k.SourceSessionId,
            k.SourceSession != null ? k.SourceSession.Task : null,
            k.CreatedAtUtc,
            k.UpdatedAtUtc,
            k.LastUsedAtUtc);

    private sealed record KnowledgeLibraryCursor(
        string Version,
        KnowledgeStatus Status,
        string? Search,
        double? Confidence,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset UpdatedAtUtc,
        Guid Id);
}
