using System.Net;
using System.Text.RegularExpressions;
using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Application.Localization;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;
using DomainEntity = AgentContext.Domain.Entities.Domain;

namespace AgentContext.Application.Skills;

/// <inheritdoc cref="ISkillAppService"/>
/// <summary>
/// Thin Skill management (T6 / spec US21–23): one row per published version so
/// history is retained; get_skill resolves the latest. Domain resolution follows
/// the single-user MVP convention (first Workspace, resolve-or-create by name),
/// consistent with save_session and retrieval.
/// </summary>
public sealed class SkillAppService(AgentContextDbContext db) : ISkillAppService
{
    private static readonly Regex SlugPattern = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);

    public async Task<SkillDetail> CreateAsync(CreateSkillRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ValidateSlug(request.Slug);

        var domain = await ResolveDomainAsync(request.Domain, cancellationToken);

        var exists = await db.Skills.AnyAsync(
            s => s.WorkspaceId == domain.WorkspaceId && s.DomainId == domain.Id && s.Slug == request.Slug,
            cancellationToken);
        if (exists)
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.SlugExists, request.Slug, domain.Name);
        }

        var now = DateTimeOffset.UtcNow;
        var skill = new Skill
        {
            WorkspaceId = domain.WorkspaceId,
            DomainId = domain.Id,
            Slug = request.Slug,
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            Instructions = request.Instructions.Trim(),
            Version = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        db.Skills.Add(skill);
        await db.SaveChangesAsync(cancellationToken);

        return ToDetail(skill, domain.Name);
    }

    public async Task<IReadOnlyList<SkillListItem>> ListAsync(CancellationToken cancellationToken = default)
    {
        // Latest version per (domain, slug) — group by the identity, order by Version
        // inside the group, then surface newest-updated first.
        var workspaceId = await FirstWorkspaceIdAsync(cancellationToken);
        if (workspaceId is null)
        {
            return [];
        }

        var rows = await db.Skills.AsNoTracking()
            .Where(s => s.WorkspaceId == workspaceId)
            .GroupBy(s => new { s.DomainId, s.Slug })
            .Select(g => g.OrderByDescending(s => s.Version).First())
            .ToListAsync(cancellationToken);

        var domainIds = rows.Select(r => r.DomainId).Distinct().ToList();
        var domainNames = await db.Domains.AsNoTracking()
            .Where(d => domainIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name, cancellationToken);

        return rows
            .OrderByDescending(s => s.UpdatedAtUtc)
            .Select(s => new SkillListItem(
                s.Id,
                domainNames.GetValueOrDefault(s.DomainId, "unknown"),
                s.Slug,
                s.Name,
                s.Description,
                s.Version,
                s.CreatedAtUtc,
                s.UpdatedAtUtc))
            .ToList();
    }

    public async Task<SkillDetail> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var skill = await db.Skills.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new { s, DomainName = s.Domain != null ? s.Domain.Name : null })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new LocalizedException(HttpStatusCode.NotFound, ErrorCodes.Skill.NotFound, id);

        return ToDetail(skill.s, skill.DomainName ?? "unknown");
    }

    public async Task<SkillDetail> GetBySlugAsync(string domainName, string slug, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domainName);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var workspaceId = await FirstWorkspaceIdAsync(cancellationToken);
        if (workspaceId is null)
        {
            throw new LocalizedException(HttpStatusCode.NotFound, ErrorCodes.Skill.SlugNotFound, slug, domainName);
        }

        var skill = await db.Skills.AsNoTracking()
            .Where(s => s.WorkspaceId == workspaceId
                && s.Slug == slug
                && s.Domain != null && s.Domain.Name == domainName)
            .OrderByDescending(s => s.Version)
            .Select(s => new { s, DomainName = s.Domain!.Name })
            .FirstOrDefaultAsync(cancellationToken);

        return skill is null
            ? throw new LocalizedException(HttpStatusCode.NotFound, ErrorCodes.Skill.SlugNotFound, slug, domainName)
            : ToDetail(skill.s, skill.DomainName);
    }

    public async Task<SkillDetail> PublishAsync(Guid id, PublishSkillRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var current = await db.Skills
            .Include(s => s.Domain)
            .FirstOrDefaultAsync(s => s.Id == id, cancellationToken)
            ?? throw new LocalizedException(HttpStatusCode.NotFound, ErrorCodes.Skill.NotFound, id);

        // The next version is current-max + 1; the existing row stays as history.
        var maxVersion = await db.Skills
            .Where(s => s.WorkspaceId == current.WorkspaceId
                && s.DomainId == current.DomainId
                && s.Slug == current.Slug)
            .MaxAsync(s => (int?)s.Version, cancellationToken) ?? 0;

        var now = DateTimeOffset.UtcNow;
        var next = new Skill
        {
            WorkspaceId = current.WorkspaceId,
            DomainId = current.DomainId,
            Slug = current.Slug,
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            Instructions = request.Instructions.Trim(),
            Version = maxVersion + 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        db.Skills.Add(next);
        await db.SaveChangesAsync(cancellationToken);

        return ToDetail(next, current.Domain.Name);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var target = await db.Skills
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new { s.WorkspaceId, s.DomainId, s.Slug })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new LocalizedException(HttpStatusCode.NotFound, ErrorCodes.Skill.NotFound, id);

        // Deleting a skill removes every version of its (domain, slug): get_skill
        // must no longer resolve it, so all history rows go.
        var deleted = await db.Skills
            .Where(s => s.WorkspaceId == target.WorkspaceId
                && s.DomainId == target.DomainId
                && s.Slug == target.Slug)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted == 0)
        {
            throw new LocalizedException(HttpStatusCode.NotFound, ErrorCodes.Skill.NotFound, id);
        }
    }

    private async Task<DomainEntity> ResolveDomainAsync(string name, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var trimmed = name.Trim();

        var workspaceId = await FirstWorkspaceIdAsync(cancellationToken)
            ?? throw new LocalizedException(HttpStatusCode.InternalServerError, ErrorCodes.Platform.NotConfigured);

        var domain = await db.Domains.FirstOrDefaultAsync(
            d => d.WorkspaceId == workspaceId && d.Name == trimmed, cancellationToken);
        if (domain is null)
        {
            domain = new DomainEntity { WorkspaceId = workspaceId, Name = trimmed, IsShared = false };
            db.Domains.Add(domain);
            await db.SaveChangesAsync(cancellationToken);
        }

        return domain;
    }

    private async Task<Guid?> FirstWorkspaceIdAsync(CancellationToken cancellationToken)
        => await db.Workspaces.AsNoTracking()
            .OrderBy(w => w.CreatedAtUtc)
            .Select(w => (Guid?)w.Id)
            .FirstOrDefaultAsync(cancellationToken);

    private static void ValidateSlug(string slug)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);
        if (!SlugPattern.IsMatch(slug))
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.SlugInvalid);
        }
    }

    private static SkillDetail ToDetail(Skill skill, string domainName) => new(
        skill.Id,
        domainName,
        skill.Slug,
        skill.Name,
        skill.Description,
        skill.Instructions,
        skill.Version,
        skill.CreatedAtUtc,
        skill.UpdatedAtUtc);
}
