using AgentContext.Application.Dtos;

namespace AgentContext.Application.Contracts;

/// <summary>
/// The filesystem side of a Skill package (T12): a Skill is a directory of files
/// (<c>SKILL.md</c> plus code examples / tools / assets) under the configured
/// skills data directory, while the DB keeps metadata. All paths are relative to
/// the package root and path-traversal is rejected. Content flows as raw bytes so
/// binary assets are supported; the store never touches the database.
/// </summary>
public interface ISkillPackageStore
{
    /// <summary>
    /// Returns the package root directory, creating it when missing. When the
    /// package has no <c>SKILL.md</c> yet, one is created from
    /// <paramref name="fallbackMarkdown"/> (the legacy Instructions column during
    /// migration) or empty — idempotent, so existing packages are left untouched.
    /// </summary>
    string EnsurePackage(string domainName, string slug, int version, string? fallbackMarkdown = null);

    /// <summary>Returns whether a concrete version directory already exists.</summary>
    bool PackageExists(string domainName, string slug, int version);

    /// <summary>Creates a fresh package (used by create/publish) with SKILL.md from the input markdown.</summary>
    void CreatePackage(string domainName, string slug, int version, string? initialMarkdown);

    /// <summary>All files in the package, relative paths, ordered by name.</summary>
    IReadOnlyList<SkillFileInfo> ListFiles(string domainName, string slug, int version);

    /// <summary>All persistent folders, including folders without files.</summary>
    IReadOnlyList<string> ListFolders(string domainName, string slug, int version);

    /// <summary>Reads a file's raw bytes.</summary>
    byte[] ReadFile(string domainName, string slug, int version, string path);

    /// <summary>Writes (creates or overwrites) a file.</summary>
    void WriteFile(string domainName, string slug, int version, string path, byte[] content);

    /// <summary>Deletes a file.</summary>
    void DeleteFile(string domainName, string slug, int version, string path);

    /// <summary>
    /// Copies every file of an existing package into a new version directory
    /// (publish keeps the full package); optionally overrides the main file.
    /// </summary>
    void CopyPackage(
        string sourceDomain, string sourceSlug, int sourceVersion,
        string targetDomain, string targetSlug, int targetVersion,
        string? overrideMain = null);

    /// <summary>
    /// Builds and atomically installs a new package directory from a source
    /// version plus staged changes. The source package is never modified.
    /// </summary>
    bool PublishPackage(
        string sourceDomain,
        string sourceSlug,
        int sourceVersion,
        string targetDomain,
        string targetSlug,
        int targetVersion,
        string instructions,
        PublishSkillVersionRequest changes,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes the whole package directory (used by Skill deletion).</summary>
    void DeletePackage(string domainName, string slug, int version);

    /// <summary>Creates a new package by safely extracting a ZIP archive.</summary>
    Task CreatePackageFromZipAsync(
        string domainName,
        string slug,
        int version,
        Stream zipStream,
        CancellationToken cancellationToken = default);

    /// <summary>Extracts a ZIP archive into an existing package.</summary>
    Task ImportZipAsync(
        string domainName,
        string slug,
        int version,
        Stream zipStream,
        CancellationToken cancellationToken = default);
}
