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
/// Skill package management: uploaded packages are stored in the filesystem and
/// metadata is stored in the database. Domain resolution follows the single-user
/// MVP convention (first Workspace, resolve-or-create by name).
/// </summary>
public sealed class SkillAppService(AgentContextDbContext db, ISkillPackageStore packages) : ISkillAppService
{
    private const int DefaultSkillPageSize = 20;
    private const int MaxSkillPageSize = 100;
    private const string ZipSourceType = "zip";
    private const string FileSourceType = "file";

    private static readonly string[] SupportedListSorts =
    [
        "updated-desc",
        "updated-asc",
        "name-asc",
        "name-desc",
        "version-desc",
        "version-asc",
    ];

    private static readonly Regex SlugPattern = new("^[a-z0-9]+(-[a-z0-9]+)*$", RegexOptions.Compiled);
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

            return await ToDetailAsync(skill, domain.Name, isLatest: true, cancellationToken);
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

    public async Task<SkillDetail> CreateFromFileAsync(
        CreateSkillFromFileRequest request,
        Stream fileStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(fileStream);
        cancellationToken.ThrowIfCancellationRequested();

        var slug = request.Slug.Trim();
        ValidateSlug(slug);
        ValidateName(request.Name);

        var fileName = request.FileName.Replace('\\', '/').Trim();
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains('/') || fileName is "." or "..")
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.FilePathInvalid);
        }

        using var buffer = new MemoryStream();
        await fileStream.CopyToAsync(buffer, cancellationToken);
        var content = buffer.ToArray();
        var isMainFile = string.Equals(fileName, "SKILL.md", StringComparison.OrdinalIgnoreCase);

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
            var instructions = isMainFile ? Encoding.UTF8.GetString(content) : string.Empty;
            packages.CreatePackage(domain.Name, slug, version, instructions);
            packageCreated = true;
            if (!isMainFile)
            {
                packages.AddFile(domain.Name, slug, version, fileName, content);
            }

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
                SourceType = FileSourceType,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };

            db.Skills.Add(skill);
            await db.SaveChangesAsync(cancellationToken);
            packageCreated = false;

            return await ToDetailAsync(skill, domain.Name, isLatest: true, cancellationToken);
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
        => await ListAsync(new SkillListQuery(pageSize, cursor), cancellationToken);

    public async Task<SkillListPage> ListAsync(
        SkillListQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        var effectivePageSize = query.PageSize ?? DefaultSkillPageSize;
        if (effectivePageSize is < 1 or > MaxSkillPageSize)
        {
            throw new LocalizedException(
                HttpStatusCode.BadRequest,
                ErrorCodes.Skill.PageSizeInvalid,
                DefaultSkillPageSize,
                MaxSkillPageSize);
        }

        var search = NormalizeListFilter(query.Search);
        var domain = NormalizeListFilter(query.Domain);
        var sourceType = NormalizeListFilter(query.SourceType);
        var sort = ParseListSort(query.Sort);
        var decodedCursor = DecodeCursor(query.Cursor);
        ValidateCursor(decodedCursor, sort, search, domain, sourceType);
        var workspaceId = await FirstWorkspaceIdAsync(cancellationToken);
        if (workspaceId is null)
        {
            return new SkillListPage(effectivePageSize, query.Cursor, [], false, null);
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

        if (search is not null)
        {
            latest = latest.Where(s =>
                s.Name.ToLower().Contains(search)
                || s.Slug.ToLower().Contains(search)
                || s.Description.ToLower().Contains(search));
        }

        if (domain is not null)
        {
            latest = latest.Where(s => s.Domain.Name.ToLower() == domain);
        }

        if (sourceType is not null)
        {
            latest = latest.Where(s => s.SourceType == sourceType);
        }

        if (decodedCursor is not null)
        {
            latest = ApplyCursor(latest, decodedCursor, sort);
        }

        var ordered = ApplySort(latest, sort);
        var rows = await ordered
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
            ? EncodeCursor(pageRows[^1], sort, search, domain, sourceType)
            : null;

        return new SkillListPage(effectivePageSize, query.Cursor, items, hasMore, nextCursor);
    }

    public async Task<SkillDetail> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var skill = await db.Skills.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new { s, DomainName = s.Domain != null ? s.Domain.Name : null })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new LocalizedException(HttpStatusCode.NotFound, ErrorCodes.Skill.NotFound, id);

        var latestId = await LatestIdAsync(skill.s, cancellationToken);
        return await ToDetailAsync(skill.s, skill.DomainName ?? "unknown", skill.s.Id == latestId, cancellationToken);
    }

    public async Task<SkillDetail> GetBySlugAsync(string domainName, string slug, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(domainName);
        ArgumentException.ThrowIfNullOrWhiteSpace(slug);

        var skill = await FindLatestAsync(domainName, slug, cancellationToken)
            ?? throw new LocalizedException(HttpStatusCode.NotFound, ErrorCodes.Skill.SlugNotFound, slug, domainName);

        return await ToDetailAsync(skill, domainName, true, cancellationToken);
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
            skill.CreatedAtUtc, skill.UpdatedAtUtc, manifest, files,
            packages.ListFolders(domain, skill.Slug, skill.Version));
    }

    public async Task<byte[]> ReadFileAsync(Guid id, string path, CancellationToken cancellationToken = default)
    {
        var (domainName, slug, version) = await ResolvePackageLocationAsync(id, cancellationToken);
        return packages.ReadFile(domainName, slug, version, path);
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
        Guid id,
        CancellationToken cancellationToken)
    {
        var skill = await db.Skills.AsNoTracking()
            .Where(s => s.Id == id)
            .Select(s => new { s.WorkspaceId, s.DomainId, s.Slug, s.Version, DomainName = s.Domain != null ? s.Domain.Name : null })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new LocalizedException(HttpStatusCode.NotFound, ErrorCodes.Skill.NotFound, id);

        if (string.IsNullOrEmpty(skill.DomainName))
        {
            throw new LocalizedException(HttpStatusCode.NotFound, ErrorCodes.Skill.NotFound, id);
        }

        return (skill.DomainName, skill.Slug, skill.Version);
    }

    private async Task<SkillDetail> ToDetailAsync(
        Skill skill,
        string domainName,
        bool isLatest,
        CancellationToken cancellationToken)
    {
        // Lazy migration (T12 AC5) is only allowed for the latest row. A
        // historical read must not create or otherwise mutate its package.
        if (isLatest)
        {
            packages.EnsurePackage(domainName, skill.Slug, skill.Version, skill.Instructions);
        }

        var manifest = packages.ListFiles(domainName, skill.Slug, skill.Version);
        var folders = packages.ListFolders(domainName, skill.Slug, skill.Version);

        return new SkillDetail(
            skill.Id, skill.PreviousVersionId, domainName, skill.Slug, skill.Name, skill.Description,
            skill.Version, skill.CreatedAtUtc, skill.UpdatedAtUtc, manifest, skill.SourceType, isLatest, folders);
    }

    private async Task<Guid> LatestIdAsync(Skill skill, CancellationToken cancellationToken)
        => await db.Skills
            .Where(candidate => candidate.WorkspaceId == skill.WorkspaceId
                && candidate.DomainId == skill.DomainId
                && candidate.Slug == skill.Slug)
            .OrderByDescending(candidate => candidate.Version)
            .Select(candidate => candidate.Id)
            .FirstAsync(cancellationToken);

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

    private static string? NormalizeListFilter(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToLowerInvariant();

    private static string ParseListSort(string? sort)
    {
        var normalized = string.IsNullOrWhiteSpace(sort) ? "updated-desc" : sort.Trim().ToLowerInvariant();
        if (!SupportedListSorts.Contains(normalized, StringComparer.Ordinal))
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.SortInvalid);
        }

        return normalized;
    }

    private static IQueryable<Skill> ApplyCursor(
        IQueryable<Skill> source,
        SkillListCursor cursor,
        string sort)
        => sort switch
        {
            "updated-asc" => source.Where(s =>
                s.UpdatedAtUtc > cursor.UpdatedAtUtc
                || (s.UpdatedAtUtc == cursor.UpdatedAtUtc
                    && (s.Id.CompareTo(cursor.Id) > 0
                        || (s.Id == cursor.Id && s.Version > cursor.Version)))),
            "name-asc" => source.Where(s =>
                s.Name.ToLower().CompareTo(cursor.Name) > 0
                || (s.Name.ToLower() == cursor.Name
                    && s.Id.CompareTo(cursor.Id) > 0)),
            "name-desc" => source.Where(s =>
                s.Name.ToLower().CompareTo(cursor.Name) < 0
                || (s.Name.ToLower() == cursor.Name
                    && s.Id.CompareTo(cursor.Id) < 0)),
            "version-asc" => source.Where(s =>
                s.Version > cursor.Version
                || (s.Version == cursor.Version && s.Id.CompareTo(cursor.Id) > 0)),
            "version-desc" => source.Where(s =>
                s.Version < cursor.Version
                || (s.Version == cursor.Version && s.Id.CompareTo(cursor.Id) < 0)),
            _ => source.Where(s =>
                s.UpdatedAtUtc < cursor.UpdatedAtUtc
                || (s.UpdatedAtUtc == cursor.UpdatedAtUtc
                    && (s.Id.CompareTo(cursor.Id) < 0
                        || (s.Id == cursor.Id && s.Version < cursor.Version)))),
        };

    private static IQueryable<Skill> ApplySort(IQueryable<Skill> source, string sort)
        => sort switch
        {
            "updated-asc" => source.OrderBy(s => s.UpdatedAtUtc)
                .ThenBy(s => s.Id)
                .ThenBy(s => s.Version),
            "name-asc" => source.OrderBy(s => s.Name.ToLower())
                .ThenBy(s => s.Id),
            "name-desc" => source.OrderByDescending(s => s.Name.ToLower())
                .ThenByDescending(s => s.Id),
            "version-asc" => source.OrderBy(s => s.Version)
                .ThenBy(s => s.Id),
            "version-desc" => source.OrderByDescending(s => s.Version)
                .ThenByDescending(s => s.Id),
            _ => source.OrderByDescending(s => s.UpdatedAtUtc)
                .ThenByDescending(s => s.Id)
                .ThenByDescending(s => s.Version),
        };

    private static void ValidateCursor(
        SkillListCursor? cursor,
        string sort,
        string? search,
        string? domain,
        string? sourceType)
    {
        if (cursor is null)
        {
            return;
        }

        if (!string.Equals(cursor.Sort, sort, StringComparison.Ordinal)
            || !string.Equals(cursor.Search, search, StringComparison.Ordinal)
            || !string.Equals(cursor.Domain, domain, StringComparison.Ordinal)
            || !string.Equals(cursor.SourceType, sourceType, StringComparison.Ordinal))
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.CursorInvalid);
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
            if (decoded is null
                || string.IsNullOrWhiteSpace(decoded.Sort)
                || decoded.Id == Guid.Empty
                || decoded.Version < 1
                || decoded.UpdatedAtUtc == default)
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

    private static string EncodeCursor(
        Skill skill,
        string sort,
        string? search,
        string? domain,
        string? sourceType)
    {
        var payload = JsonSerializer.SerializeToUtf8Bytes(new SkillListCursor(
            sort,
            search,
            domain,
            sourceType,
            skill.UpdatedAtUtc,
            skill.Name.ToLowerInvariant(),
            skill.Id,
            skill.Version));
        return Convert.ToBase64String(payload)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private sealed record SkillListCursor(
        string Sort,
        string? Search,
        string? Domain,
        string? SourceType,
        DateTimeOffset UpdatedAtUtc,
        string Name,
        Guid Id,
        int Version);
}
