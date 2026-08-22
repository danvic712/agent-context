using System.Net;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using DomainEntity = AgentContext.Domain.Entities.Domain;
using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Application.Localization;

namespace AgentContext.Application.Sessions;

/// <inheritdoc cref="ISaveSessionAppService"/>
public sealed class SaveSessionAppService(AgentContextDbContext db) : ISaveSessionAppService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<SaveSessionResult> SaveAsync(SaveSessionRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // US7: remembering a session means the full original context is stored.
        if (request.Remembered && string.IsNullOrWhiteSpace(request.FullContext))
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Session.FullContextRequired);
        }

        var workspace = await db.Workspaces
            .OrderBy(w => w.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new LocalizedException(HttpStatusCode.InternalServerError, ErrorCodes.Platform.NotConfigured);

        // Domain tagging is explicit; an unknown domain falls back to inference
        // by being registered on the spot (spec US6 / T2 acceptance).
        Guid? domainId = null;
        string? domainName = null;
        if (!string.IsNullOrWhiteSpace(request.Domain))
        {
            var name = request.Domain.Trim();
            var domain = await db.Domains.FirstOrDefaultAsync(
                d => d.WorkspaceId == workspace.Id && d.Name == name, cancellationToken);
            if (domain is null)
            {
                domain = new DomainEntity { WorkspaceId = workspace.Id, Name = name, IsShared = false };
                db.Domains.Add(domain);
            }

            domainId = domain.Id;
            domainName = name;
        }

        var now = DateTimeOffset.UtcNow;
        var session = new Session
        {
            WorkspaceId = workspace.Id,
            DomainId = domainId,
            AgentName = string.IsNullOrWhiteSpace(request.AgentName) ? "craft-agents" : request.AgentName,
            Task = request.Task,
            Conclusion = request.Conclusion,
            SummaryJson = JsonSerializer.Serialize(
                new { request.Task, request.Conclusion, request.KeySnippets }, JsonOptions),
            Status = SessionStatus.Pending,
            Remembered = request.Remembered,
            // Full original context is stored only when the user explicitly asks to remember.
            FullContext = request.Remembered ? request.FullContext : null,
            CreatedAtUtc = now,
        };

        // Reported Session usage is explicitly tagged so it cannot be confused
        // with Learning Engine platform usage. A missing model falls back to
        // "unknown" so the row is never silently dropped. Cost is intentionally
        // not persisted in the source-aware Usage ledger.
        if (!string.IsNullOrWhiteSpace(request.Model)
            || request.TokensIn != 0
            || request.TokensOut != 0)
        {
            var model = string.IsNullOrWhiteSpace(request.Model) ? "unknown" : request.Model;
            session.Usage.Add(new Usage
            {
                Model = model,
                Source = UsageSource.ReportedSession,
                InputTokens = request.TokensIn,
                CachedInputTokens = 0,
                OutputTokens = request.TokensOut,
                CreatedAtUtc = now,
            });
        }

        db.Sessions.Add(session);
        await db.SaveChangesAsync(cancellationToken);

        return new SaveSessionResult(session.Id, domainName, session.Remembered);
    }

    public async Task<SessionDetail> GetAsync(Guid sessionId, CancellationToken cancellationToken = default)
    {
        var session = await WithOverviewIncludes(db.Sessions)
            .SingleOrDefaultAsync(s => s.Id == sessionId, cancellationToken)
            ?? throw new LocalizedException(HttpStatusCode.NotFound, ErrorCodes.Session.NotFound, sessionId);

        return MapDetail(session);
    }

    public async Task<IReadOnlyList<SessionListItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        var sessions = await WithOverviewIncludes(db.Sessions)
            .OrderByDescending(s => s.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        return sessions.Select(s => new SessionListItem(
            s.Id,
            s.Domain?.Name,
            s.Task,
            s.Status.ToString(),
            s.Remembered,
            s.CreatedAtUtc,
            s.Usage.Sum(u => u.InputTokens + u.OutputTokens))).ToList();
    }

    private static IQueryable<Session> WithOverviewIncludes(IQueryable<Session> query)
        => query.Include(s => s.Domain).Include(s => s.Usage);

    private static SessionDetail MapDetail(Session s) => new(
        s.Id,
        s.WorkspaceId,
        s.Domain?.Name,
        s.AgentName,
        s.Task,
        s.Conclusion,
        s.SummaryJson,
        s.Status.ToString(),
        s.Remembered,
        s.CreatedAtUtc,
        s.Usage.Select(u => new SessionUsageDto(
            u.Model,
            u.InputTokens,
            u.CachedInputTokens,
            u.OutputTokens,
            u.Source == UsageSource.LearningEngine ? "learning_engine" : "reported_session",
            u.InferenceRouteId,
            u.Capability)).ToList());
}
