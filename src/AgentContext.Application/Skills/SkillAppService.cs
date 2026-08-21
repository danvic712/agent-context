using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Text.Json;
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
    private const int DefaultSkillPageSize = 20;
    private const int MaxSkillPageSize = 100;
    private const string ManualSourceType = "manual";
    private const string ZipSourceType = "zip";

    private static readonly Regex SlugPattern = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);

    public async Task<SkillDetail> CreateAsync(CreateSkillRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var slug = request.Slug.Trim();
        ValidateSlug(slug);
        ValidateName(request.Name);

        var domain = await ResolveDomainAsync(request.Domain, cancellationToken);

        var exists = await db.Skills.AnyAsync(
            s => s.WorkspaceId == domain.WorkspaceId && s.DomainId == domain.Id && s.Slug == slug,
            cancellationToken);
        if (exists)
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.SlugExists, slug, domain.Name);
        }

        var instructions = request.Instructions.Trim();
        var now = DateTimeOffset.UtcNow;
        var skill = new Skill
        {
            WorkspaceId = domain.WorkspaceId,
            DomainId = domain.Id,
            Slug = slug,
            Name = request.Name.Trim(),
            Description = request.Description.Trim(),
            Instructions = instructions,
            Version = 1,
            SourceType = ManualSourceType,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        // Stage the filesystem package first so a package failure never leaves a
        // database row pointing at content that was not created.
        packages.CreatePackage(domain.Name, skill.Slug, skill.Version, instructions);
        try
        {
            db.Skills.Add(skill);
            await db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            packages.DeletePackage(domain.Name, skill.Slug, skill.Version);
            throw;
        }

        return await ToDetailAsync(skill, domain.Name, cancellationToken);
    }

    public async Task<SkillDetail> CreateFromZipAsync(
        CreateSkillFromZipRequest request,
        Stream zipStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(zipStream);
        cancellationToken.ThrowIfCancellationRequested();

        var slug = request.Slug.Trim();
        ValidateSlug(slug);
        ValidateName(request.Name);

        var domain = await ResolveDomainAsync(request.Domain, cancellationToken);
        var exists = await db.Skills.AnyAsync(
            s => s.WorkspaceId == domain.WorkspaceId && s.DomainId == domain.Id && s.Slug == slug,
            cancellationToken);
        if (exists)
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.SlugExists, slug, domain.Name);
        }

        const int version = 1;
        var packageCreated = false;
        try
        {
            await packages.CreatePackageFromZipAsync(
                domain.Name, slug, version, zipStream, cancellationToken);
            packageCreated = true;

            var instructions = Encoding.UTF8.GetString(
                packages.ReadFile(domain.Name, slug, version, "SKILL.md"));
            var now = DateTimeOffset.UtcNow;
            var skill = new Skill
            {
                WorkspaceId = domain.WorkspaceId,
                DomainId = domain.Id,
                Slug = slug,
                Name = request.Name.Trim(),
                Description = request.Description.Trim(),
                Instructions = instructions,
                Version = version,
                SourceType = ZipSourceType,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };

            db.Skills.Add(skill);
            await db.SaveChangesAsync(cancellationToken);
            packageCreated = false;

            return await ToDetailAsync(skill, domain.Name, cancellationToken);
        }
        catch
        {
            if (packageCreated)
            {
                packages.DeletePackage(domain.Name, slug, version);
            }

            throw;
        }
    }

    public async Task<SkillListPage> ListAsync(
        int? pageSize = null,
        string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var effectivePageSize = pageSize ?? DefaultSkillPageSize;
        if (effectivePageSize is < 1 or > MaxSkillPageSize)
        {
            throw new LocalizedException(
                HttpStatusCode.BadRequest,
                ErrorCodes.Skill.PageSizeInvalid,
                DefaultSkillPageSize,
                MaxSkillPageSize);
        }

        var decodedCursor = DecodeCursor(cursor);
        var workspaceId = await FirstWorkspaceIdAsync(cancellationToken);
        if (workspaceId is null)
        {
            return new SkillListPage(effectivePageSize, cursor, [], false, null);
        }

        // Latest version per (domain, slug). A correlated MAX(version) predicate
        // keeps this query translatable by PostgreSQL while still letting the
        // database apply the cursor and page bounds before materialization.
        var latest = db.Skills.AsNoTracking()
            .Where(s => s.WorkspaceId == workspaceId)
            .Where(s => s.Version == db.Skills
                .Where(candidate => candidate.WorkspaceId == s.WorkspaceId
                    && candidate.DomainId == s.DomainId
                    && candidate.Slug == s.Slug)
                .Max(candidate => candidate.Version));

        if (decodedCursor is not null)
        {
            latest = latest.Where(s =>
                s.UpdatedAtUtc < decodedCursor.UpdatedAtUtc
                || (s.UpdatedAtUtc == decodedCursor.UpdatedAtUtc
                    && (s.Id.CompareTo(decodedCursor.Id) < 0
                        || (s.Id == decodedCursor.Id && s.Version < decodedCursor.Version))));
        }

        var rows = await latest
            .OrderByDescending(s => s.UpdatedAtUtc)
            .ThenByDescending(s => s.Id)
            .ThenByDescending(s => s.Version)
            .Take(effectivePageSize + 1)
            .ToListAsync(cancellationToken);

        var hasMore = rows.Count > effectivePageSize;
        var pageRows = rows.Take(effectivePageSize).ToList();

        var domainIds = pageRows.Select(r => r.DomainId).Distinct().ToList();
        var domainNames = await db.Domains.AsNoTracking()
            .Where(d => domainIds.Contains(d.Id))
            .ToDictionaryAsync(d => d.Id, d => d.Name, cancellationToken);

        var items = pageRows
            .Select(s => new SkillListItem(
                s.Id,
                domainNames.GetValueOrDefault(s.DomainId, "unknown"),
                s.Slug,
                s.Name,
                s.Description,
                s.Version,
                s.CreatedAtUtc,
                s.UpdatedAtUtc,
                s.SourceType))
            .ToList();

        var nextCursor = hasMore && pageRows.Count > 0
            ? EncodeCursor(pageRows[^1])
            : null;

        return new SkillListPage(effectivePageSize, cursor, items, hasMore, nextCursor);
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
            Instructions = request.Instructions.Trim(),
            Version = maxVersion + 1,
            SourceType = current.SourceType,
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
        await packages.ImportZipAsync(domainName, slug, version, zipStream, cancellationToken);
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
            skill.Version, skill.CreatedAtUtc, skill.UpdatedAtUtc, manifest, skill.SourceType);
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

    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.NameRequired);
        }
    }

    private static SkillListCursor? DecodeCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
        {
            return null;
        }

        try
        {
            var base64 = cursor.Replace('-', '+').Replace('_', '/');
            base64 = base64.PadRight(base64.Length + (4 - base64.Length % 4) % 4, '=');
            var decoded = JsonSerializer.Deserialize<SkillListCursor>(Convert.FromBase64String(base64));
            if (decoded is null || decoded.Id == Guid.Empty || decoded.Version < 1 || decoded.UpdatedAtUtc == default)
            {
                throw new JsonException("The cursor payload is incomplete.");
            }

            return decoded;
        }
        catch (FormatException)
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.CursorInvalid);
        }
        catch (JsonException)
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.CursorInvalid);
        }
    }

    private static string EncodeCursor(Skill skill)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new SkillListCursor(
            skill.UpdatedAtUtc,
            skill.Id,
            skill.Version));
        return Convert.ToBase64String(payload)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private sealed record SkillListCursor(DateTimeOffset UpdatedAtUtc, Guid Id, int Version);
}
