using System.IO.Compression;
using System.Net;
using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Application.Localization;

namespace AgentContext.Application.Skills;

/// <inheritdoc cref="ISkillPackageStore"/>
/// <summary>
/// Filesystem-backed Skill packages (T12): <c>{root}/{domain}/{slug}/v{version}/</c>.
/// Paths are normalized and traversal is rejected; file contents are raw bytes so
/// binary assets work. A package always carries a <c>SKILL.md</c> main file —
/// created from the legacy Instructions value during migration when missing.
/// </summary>
public sealed class SkillPackageStore(string rootDirectory) : ISkillPackageStore
{
    private const string MainFile = "SKILL.md";
    private const long MaxFileBytes = 10 * 1024 * 1024; // 10 MB

    /// <summary>Resolved absolute root of the skills data directory.</summary>
    public string RootDirectory => _root;

    private readonly string _root = Path.GetFullPath(rootDirectory);

    public string EnsurePackage(string domainName, string slug, int version, string? fallbackMarkdown = null)
    {
        var dir = PackageDirectory(domainName, slug, version);
        Directory.CreateDirectory(dir);

        var mainPath = Path.Combine(dir, MainFile);
        if (!File.Exists(mainPath))
        {
            File.WriteAllText(mainPath, fallbackMarkdown ?? string.Empty);
        }

        return dir;
    }

    public void CreatePackage(string domainName, string slug, int version, string? initialMarkdown)
    {
        var dir = PackageDirectory(domainName, slug, version);
        if (Directory.Exists(dir))
        {
            throw new LocalizedException(HttpStatusCode.Conflict, ErrorCodes.Skill.PackageExists, domainName, slug, version);
        }

        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, MainFile), initialMarkdown ?? string.Empty);
    }

    public IReadOnlyList<SkillFileInfo> ListFiles(string domainName, string slug, int version)
    {
        var dir = PackageDirectory(domainName, slug, version);
        if (!Directory.Exists(dir))
        {
            return [];
        }

        return Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories)
            .Select(file => new SkillFileInfo(
                Path.GetRelativePath(dir, file).Replace(Path.DirectorySeparatorChar, '/'),
                new FileInfo(file).Length,
                IsBinary(file)))
            .OrderBy(f => f.Path, StringComparer.Ordinal)
            .ToList();
    }

    public byte[] ReadFile(string domainName, string slug, int version, string path)
    {
        var file = ResolveFile(domainName, slug, version, path);
        if (!File.Exists(file))
        {
            throw new LocalizedException(HttpStatusCode.NotFound, ErrorCodes.Skill.FileNotFound, path);
        }

        return File.ReadAllBytes(file);
    }

    public void WriteFile(string domainName, string slug, int version, string path, byte[] content)
    {
        if (content.Length > MaxFileBytes)
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.FileTooLarge, path);
        }

        var file = ResolveFile(domainName, slug, version, path);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllBytes(file, content);
    }

    public void DeleteFile(string domainName, string slug, int version, string path)
    {
        var file = ResolveFile(domainName, slug, version, path);
        if (!File.Exists(file))
        {
            throw new LocalizedException(HttpStatusCode.NotFound, ErrorCodes.Skill.FileNotFound, path);
        }

        File.Delete(file);
        TryPruneEmptyDirectories(file);
    }

    /// <summary>
    /// Copies every file of an existing package into a new version directory
    /// (publish keeps the full package, not just SKILL.md); optionally overrides
    /// the main file with edited instructions.
    /// </summary>
    public void CopyPackage(
        string sourceDomain, string sourceSlug, int sourceVersion,
        string targetDomain, string targetSlug, int targetVersion,
        string? overrideMain = null)
    {
        var source = PackageDirectory(sourceDomain, sourceSlug, sourceVersion);
        if (!Directory.Exists(source))
        {
            throw new LocalizedException(HttpStatusCode.NotFound, ErrorCodes.Skill.FileNotFound, "SKILL.md");
        }

        var target = PackageDirectory(targetDomain, targetSlug, targetVersion);
        Directory.CreateDirectory(target);

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var destination = Path.Combine(target, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            File.Copy(file, destination, overwrite: true);
        }

        if (overrideMain is not null)
        {
            File.WriteAllText(Path.Combine(target, MainFile), overrideMain);
        }
    }

    public void DeletePackage(string domainName, string slug, int version)
    {
        var dir = PackageDirectory(domainName, slug, version);
        if (Directory.Exists(dir))
        {
            Directory.Delete(dir, recursive: true);
        }

        // Prune empty parent directories (slug / domain) so nothing lingers.
        var parent = Path.GetDirectoryName(dir);
        while (parent is not null
               && !string.Equals(parent, _root, StringComparison.Ordinal)
               && Directory.Exists(parent)
               && !Directory.EnumerateFileSystemEntries(parent).Any())
        {
            Directory.Delete(parent);
            parent = Path.GetDirectoryName(parent);
        }
    }

    public void ImportZip(string domainName, string slug, int version, Stream zipStream)
    {
        var dir = PackageDirectory(domainName, slug, version);
        Directory.CreateDirectory(dir);

        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read);
        foreach (var entry in archive.Entries)
        {
            // Directories appear as entries ending in '/' — skip them.
            if (entry.FullName.EndsWith('/'))
            {
                continue;
            }

            var relative = NormalizePath(entry.FullName);
            if (relative.Length == 0)
            {
                continue;
            }

            // Zip-slip guard: the resolved target must stay inside the package.
            var target = Path.GetFullPath(Path.Combine(dir, relative));
            if (!target.StartsWith(dir, StringComparison.Ordinal))
            {
                throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.ImportInvalid);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            using var entryStream = entry.Open();
            using var output = File.Create(target);
            entryStream.CopyTo(output);
        }

        // A package must carry SKILL.md; create an empty one when the zip omits it.
        var mainPath = Path.Combine(dir, MainFile);
        if (!File.Exists(mainPath))
        {
            File.WriteAllText(mainPath, string.Empty);
        }
    }

    private string ResolveFile(string domainName, string slug, int version, string path)
    {
        var dir = PackageDirectory(domainName, slug, version);
        if (!Directory.Exists(dir))
        {
            throw new LocalizedException(HttpStatusCode.NotFound, ErrorCodes.Skill.FileNotFound, path);
        }

        var relative = NormalizePath(path);
        if (relative.Length == 0)
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.FilePathInvalid);
        }

        var full = Path.GetFullPath(Path.Combine(dir, relative));
        if (!full.StartsWith(dir, StringComparison.Ordinal))
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.FilePathInvalid);
        }

        return full;
    }

    private string PackageDirectory(string domainName, string slug, int version)
    {
        var domain = SanitizeSegment(domainName);
        var slugPart = SanitizeSegment(slug);
        return Path.Combine(_root, domain, slugPart, $"v{version}");
    }

    private static string SanitizeSegment(string value)
    {
        var trimmed = (value ?? string.Empty).Trim().Trim('.');
        if (trimmed.Length == 0)
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.FilePathInvalid);
        }

        // A segment must not contain separators or traversal — it becomes a single
        // directory name on disk.
        if (trimmed.Contains('/') || trimmed.Contains('\\') || trimmed.Contains(".."))
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.FilePathInvalid);
        }

        return trimmed;
    }

    private static string NormalizePath(string path)
    {
        var raw = (path ?? string.Empty).Trim().Replace('\\', '/');

        // Reject traversal and rooted/absolute paths before trimming a leading '/'.
        if (raw.Length == 0 || raw.Contains("..", StringComparison.Ordinal) || Path.IsPathRooted(raw))
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.FilePathInvalid);
        }

        return raw.TrimStart('/').Trim();
    }

    private static bool IsBinary(string file)
    {
        var bytes = new byte[512];
        using var stream = File.OpenRead(file);
        var read = stream.Read(bytes, 0, bytes.Length);
        for (var i = 0; i < read; i++)
        {
            if (bytes[i] == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static void TryPruneEmptyDirectories(string file)
    {
        var dir = Path.GetDirectoryName(file);
        while (dir is not null)
        {
            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
            {
                Directory.Delete(dir);
                dir = Path.GetDirectoryName(dir);
            }
            else
            {
                break;
            }
        }
    }
}
