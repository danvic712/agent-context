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

    /// <summary>Creates a fresh package with SKILL.md from the input markdown.</summary>
    void CreatePackage(string domainName, string slug, int version, string? initialMarkdown);

    /// <summary>All files in the package, relative paths, ordered by name.</summary>
    IReadOnlyList<SkillFileInfo> ListFiles(string domainName, string slug, int version);

    /// <summary>All persistent folders, including folders without files.</summary>
    IReadOnlyList<string> ListFolders(string domainName, string slug, int version);

    /// <summary>Reads a file's raw bytes.</summary>
    byte[] ReadFile(string domainName, string slug, int version, string path);

    /// <summary>Creates a ZIP archive from the persisted package files.</summary>
    Task<Stream> CreatePackageArchiveAsync(
        string domainName,
        string slug,
        int version,
        CancellationToken cancellationToken = default);

    /// <summary>Adds the uploaded file to a new package.</summary>
    void AddFile(string domainName, string slug, int version, string path, byte[] content);

    /// <summary>Deletes a staged package directory after a failed creation.</summary>
    void DeletePackage(string domainName, string slug, int version);

    /// <summary>Creates a new package by safely extracting a ZIP archive.</summary>
    Task CreatePackageFromZipAsync(
        string domainName,
        string slug,
        int version,
        Stream zipStream,
        CancellationToken cancellationToken = default);

}
