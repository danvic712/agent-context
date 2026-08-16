using AgentContext.Application.Dtos;
using AgentContext.Application.Localization;

namespace AgentContext.Application.Contracts;

/// <summary>
/// Skill management (T6 / spec US21–23 + T12 package model): a Skill is a
/// filesystem package (<c>SKILL.md</c> plus code/assets) with metadata in the DB.
/// CRUD over the latest version plus publish-new-version (older versions are kept
/// as history), per-file operations, zip import, and get_skill by (domain, slug).
/// </summary>
public interface ISkillAppService
{
    /// <summary>
    /// Creates a Skill at version 1 with an initial package (SKILL.md from the
    /// request's Instructions). Throws <see cref="LocalizedException"/> (400,
    /// <c>skill.slugExists</c>) when the slug already exists in the domain.
    /// </summary>
    Task<SkillDetail> CreateAsync(CreateSkillRequest request, CancellationToken cancellationToken = default);

    /// <summary>All Skills in the platform, newest version per (domain, slug) first.</summary>
    Task<IReadOnlyList<SkillListItem>> ListAsync(CancellationToken cancellationToken = default);

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
    /// Publishes a new version on top of the skill with the given id: the row is
    /// kept as history and a new row with Version+1 is inserted, with its own
    /// package directory seeded from the request's Instructions.
    /// </summary>
    Task<SkillDetail> PublishAsync(Guid id, PublishSkillRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the skill (every version of its (domain, slug), including their
    /// package directories) so get_skill can no longer resolve it.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Resolves the full package (manifest + all file contents) for agent consumption.</summary>
    Task<SkillPackage> GetPackageAsync(string domain, string slug, CancellationToken cancellationToken = default);

    /// <summary>Reads one package file as raw bytes.</summary>
    Task<byte[]> ReadFileAsync(Guid id, string path, CancellationToken cancellationToken = default);

    /// <summary>Writes (creates or overwrites) one package file.</summary>
    Task<SkillDetail> WriteFileAsync(Guid id, string path, byte[] content, CancellationToken cancellationToken = default);

    /// <summary>Deletes one package file.</summary>
    Task<SkillDetail> DeleteFileAsync(Guid id, string path, CancellationToken cancellationToken = default);

    /// <summary>Imports a zip archive into the package (import flow).</summary>
    Task<SkillDetail> ImportZipAsync(Guid id, Stream zipStream, CancellationToken cancellationToken = default);
}
