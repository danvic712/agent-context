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
/// Skill management (T6 / US21–23 + T12 package model): one DB row per published
/// version (history retained) whose files live in a filesystem package
/// (<see cref="ISkillPackageStore"/>). Domain resolution follows the single-user
/// MVP convention (first Workspace, resolve-or-create by name).
/// </summary>
public sealed class SkillAppService(AgentContextDbContext db, ISkillPackageStore packages) : ISkillAppService
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
            Version = 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        db.Skills.Add(skill);
        await db.SaveChangesAsync(cancellationToken);

        // The package is the source of truth for content: seed SKILL.md from the
        // request's Instructions (legacy shape), then return the manifest.
        packages.CreatePackage(domain.Name, skill.Slug, skill.Version, request.Instructions.Trim());

        return await ToDetailAsync(skill, domain.Name, cancellationToken);
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

        return await ToDetailAsync(skill.s, skill.DomainName ?? "unknown", cancellationToken);
    }

    public async Task<SkillDetail> GetBySlugAsync(string domainName, string slug, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domainName);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var skill = await FindLatestAsync(domainName, slug, cancellationToken)
            ?? throw new LocalizedException(HttpStatusCode.NotFound, ErrorCodes.Skill.SlugNotFound, slug, domainName);

        return await ToDetailAsync(skill, domainName, cancellationToken);
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
            Version = maxVersion + 1,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };
        db.Skills.Add(next);
        await db.SaveChangesAsync(cancellationToken);

        // The new version inherits the whole package (all files), with SKILL.md
        // overridden by the edited instructions.
        packages.CopyPackage(
            current.Domain.Name, current.Slug, current.Version,
            current.Domain.Name, next.Slug, next.Version,
            request.Instructions.Trim());

        return await ToDetailAsync(next, current.Domain.Name, cancellationToken);
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
        // must no longer resolve it, so all history rows and their packages go.
        var versions = await db.Skills
            .AsNoTracking()
            .Where(s => s.WorkspaceId == target.WorkspaceId
                && s.DomainId == target.DomainId
                && s.Slug == target.Slug)
            .Select(s => s.Version)
            .ToListAsync(cancellationToken);

        var deleted = await db.Skills
            .Where(s => s.WorkspaceId == target.WorkspaceId
                && s.DomainId == target.DomainId
                && s.Slug == target.Slug)
            .ExecuteDeleteAsync(cancellationToken);

        if (deleted == 0)
        {
            throw new LocalizedException(HttpStatusCode.NotFound, ErrorCodes.Skill.NotFound, id);
        }

        var domainName = await db.Domains.AsNoTracking()
            .Where(d => d.Id == target.DomainId)
            .Select(d => d.Name)
            .FirstOrDefaultAsync(cancellationToken) ?? "unknown";

        foreach (var version in versions)
        {
            packages.DeletePackage(domainName, target.Slug, version);
        }
    }

    public async Task<SkillPackage> GetPackageAsync(string domain, string slug, CancellationToken cancellationToken = default)
    {
        var skill = await FindLatestAsync(domain, slug, cancellationToken)
            ?? throw new LocalizedException(HttpStatusCode.NotFound, ErrorCodes.Skill.SlugNotFound, slug, domain);

        packages.EnsurePackage(domain, skill.Slug, skill.Version, skill.Instructions);

        var manifest = packages.ListFiles(domain, skill.Slug, skill.Version);
        var files = manifest
            .Select(info =>
            {
                var bytes = packages.ReadFile(domain, skill.Slug, skill.Version, info.Path);
                return new SkillFileContent(
                    info.Path,
                    info.Binary ? Convert.ToBase64String(bytes) : System.Text.Encoding.UTF8.GetString(bytes),
                    info.Binary);
            })
            .ToList();

        return new SkillPackage(
            skill.Id, domain, skill.Slug, skill.Name, skill.Description, skill.Version,
            skill.CreatedAtUtc, skill.UpdatedAtUtc, manifest, files);
    }

    public async Task<byte[]> ReadFileAsync(Guid id, string path, CancellationToken cancellationToken = default)
    {
        var (domainName, slug, version) = await ResolvePackageLocationAsync(id, cancellationToken);
        return packages.ReadFile(domainName, slug, version, path);
    }

    public async Task<SkillDetail> WriteFileAsync(Guid id, string path, byte[] content, CancellationToken cancellationToken = default)
    {
        var (domainName, slug, version) = await ResolvePackageLocationAsync(id, cancellationToken);
        packages.WriteFile(domainName, slug, version, path, content);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<SkillDetail> DeleteFileAsync(Guid id, string path, CancellationToken cancellationToken = default)
    {
        var (domainName, slug, version) = await ResolvePackageLocationAsync(id, cancellationToken);
        packages.DeleteFile(domainName, slug, version, path);
        return await GetAsync(id, cancellationToken);
    }

    public async Task<SkillDetail> ImportZipAsync(Guid id, Stream zipStream, CancellationToken cancellationToken = default)
    {
        var (domainName, slug, version) = await ResolvePackageLocationAsync(id, cancellationToken);
        packages.ImportZip(domainName, slug, version, zipStream);
        return await GetAsync(id, cancellationToken);
    }

    private async Task<Skill?> FindLatestAsync(string domainName, string slug, CancellationToken cancellationToken)
    {
        var workspaceId = await FirstWorkspaceIdAsync(cancellationToken);
        if (workspaceId is null)
        {
            return null;
        }

        return await db.Skills.AsNoTracking()
            .Where(s => s.WorkspaceId == workspaceId
                && s.Slug == slug
                && s.Domain != null && s.Domain.Name == domainName)
            .OrderByDescending(s => s.Version)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private async Task<(string DomainName, string Slug, int Version)> ResolvePackageLocationAsync(
        Guid id, CancellationToken cancellationToken)
    {
        var skill = await db.Skills.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new { s.Slug, s.Version, DomainName = s.Domain != null ? s.Domain.Name : null })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new LocalizedException(HttpStatusCode.NotFound, ErrorCodes.Skill.NotFound, id);

        if (string.IsNullOrEmpty(skill.DomainName))
        {
            throw new LocalizedException(HttpStatusCode.NotFound, ErrorCodes.Skill.NotFound, id);
        }

        return (skill.DomainName, skill.Slug, skill.Version);
    }

    private async Task<SkillDetail> ToDetailAsync(Skill skill, string domainName, CancellationToken cancellationToken)
    {
        // Lazy migration (T12 AC5): a package missing its directory is seeded from
        // the legacy Instructions column — idempotent, so nothing is lost.
        packages.EnsurePackage(domainName, skill.Slug, skill.Version, skill.Instructions);
        var manifest = packages.ListFiles(domainName, skill.Slug, skill.Version);

        return new SkillDetail(
            skill.Id, domainName, skill.Slug, skill.Name, skill.Description,
            skill.Version, skill.CreatedAtUtc, skill.UpdatedAtUtc, manifest);
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
}
