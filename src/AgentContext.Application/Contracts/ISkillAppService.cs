using AgentContext.Application.Dtos;
using AgentContext.Application.Localization;

namespace AgentContext.Application.Contracts;

/// <summary>
/// Skill package management: uploaded ZIP or single-file packages are persisted
/// to the filesystem with metadata in the DB.
/// </summary>
public interface ISkillAppService
{
    /// <summary>
    /// Creates a Skill at version 1 from an uploaded ZIP package. The package is
    /// validated and staged before metadata is persisted.
    /// </summary>
    Task<SkillDetail> CreateFromZipAsync(
        CreateSkillFromZipRequest request,
        Stream zipStream,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a Skill at version 1 from one uploaded file. Non-markdown files
    /// are added beside an empty SKILL.md package entry.
    /// </summary>
    Task<SkillDetail> CreateFromFileAsync(
        CreateSkillFromFileRequest request,
        Stream fileStream,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns one bounded page of the latest version per (domain, slug), ordered
    /// by newest update with a stable cursor tie-breaker. When <paramref name="pageSize"/>
    /// is omitted, the service uses the default page size of 20; values above 100
    /// are rejected with a localized application error.
    /// </summary>
    Task<SkillListPage> ListAsync(
        int? pageSize = null,
        string? cursor = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns a bounded page using the installed-library filters and ordering.
    /// The cursor must be reused with the same query values.
    /// </summary>
    Task<SkillListPage> ListAsync(
        SkillListQuery query,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// get_skill: the latest published version of the skill at (domain, slug),
    /// with its package file manifest. Missing packages migrate lazily from the
    /// legacy Instructions column. Throws <see cref="LocalizedException"/> (404,
    /// <c>skill.slugNotFound</c>) when no such skill exists.
    /// </summary>
    Task<SkillDetail> GetBySlugAsync(string domain, string slug, CancellationToken cancellationToken = default);

    /// <summary>The Skill row with the given id and its package file manifest.</summary>
    Task<SkillDetail> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the complete skill identified by any version row, including all
    /// historical versions and their filesystem package directories.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Resolves the full package (manifest + all file contents) for agent consumption.</summary>
    Task<SkillPackage> GetPackageAsync(string domain, string slug, CancellationToken cancellationToken = default);

    /// <summary>Reads one package file as raw bytes.</summary>
    Task<byte[]> ReadFileAsync(Guid id, string path, CancellationToken cancellationToken = default);

    /// <summary>Returns the persisted Skill package as a downloadable ZIP archive.</summary>
    Task<SkillPackageDownload> DownloadPackageAsync(Guid id, CancellationToken cancellationToken = default);
}
