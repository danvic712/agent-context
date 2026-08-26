using System.IO.Compression;
using System.Net;
using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Application.Localization;

namespace AgentContext.Application.Skills;

/// <inheritdoc cref="ISkillPackageStore"/>
/// <summary>
/// Filesystem-backed uploaded Skill packages: <c>{root}/{domain}/{slug}/v{version}/</c>.
/// Paths are normalized and traversal is rejected; file contents are raw bytes so
/// binary assets work. A package always carries a <c>SKILL.md</c> main file —
/// created from the legacy Instructions value during migration when missing.
/// </summary>
public sealed class SkillPackageStore(string rootDirectory) : ISkillPackageStore
{
    private const string MainFile = "SKILL.md";
    private const long MaxFileBytes = 10 * 1024 * 1024; // 10 MB
    private const long MaxPackageBytes = 50 * 1024 * 1024; // 50 MB

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

    public IReadOnlyList<string> ListFolders(string domainName, string slug, int version)
    {
        var dir = PackageDirectory(domainName, slug, version);
        if (!Directory.Exists(dir))
        {
            return [];
        }

        return Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories)
            .Select(folder => Path.GetRelativePath(dir, folder).Replace(Path.DirectorySeparatorChar, '/'))
            .Where(path => path.Length > 0)
            .OrderBy(path => path, StringComparer.Ordinal)
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

    public async Task<Stream> CreatePackageArchiveAsync(
        string domainName,
        string slug,
        int version,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var directory = PackageDirectory(domainName, slug, version);
        if (!Directory.Exists(directory))
        {
            throw new LocalizedException(HttpStatusCode.NotFound, ErrorCodes.Skill.FileNotFound, "package");
        }

        var archiveStream = new MemoryStream();
        try
        {
            using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, leaveOpen: true))
            {
                foreach (var file in Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories)
                    .OrderBy(file => file, StringComparer.Ordinal))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var relativePath = Path.GetRelativePath(directory, file)
                        .Replace(Path.DirectorySeparatorChar, '/');
                    var entry = archive.CreateEntry(relativePath, CompressionLevel.Fastest);
                    await using var input = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.Read);
                    await using var output = entry.Open();
                    await input.CopyToAsync(output, cancellationToken);
                }
            }

            archiveStream.Position = 0;
            return archiveStream;
        }
        catch
        {
            await archiveStream.DisposeAsync();
            throw;
        }
    }

    public void AddFile(string domainName, string slug, int version, string path, byte[] content)
    {
        if (content.Length > MaxFileBytes)
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.FileTooLarge, path);
        }

        var file = ResolveFile(domainName, slug, version, path);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllBytes(file, content);
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

    public async Task CreatePackageFromZipAsync(
        string domainName,
        string slug,
        int version,
        Stream zipStream,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(zipStream);
        cancellationToken.ThrowIfCancellationRequested();

        var target = PackageDirectory(domainName, slug, version);
        if (Directory.Exists(target))
        {
            throw new LocalizedException(HttpStatusCode.Conflict, ErrorCodes.Skill.PackageExists, domainName, slug, version);
        }

        var staging = CreateStagingDirectory();
        try
        {
            await ExtractZipAsync(
                staging,
                zipStream,
                normalizeWrapper: true,
                enforceLimits: true,
                seedMainFile: true,
                cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            try
            {
                Directory.Move(staging, target);
                staging = string.Empty;
            }
            catch (IOException) when (Directory.Exists(target))
            {
                throw new LocalizedException(HttpStatusCode.Conflict, ErrorCodes.Skill.PackageExists, domainName, slug, version);
            }
        }
        catch (LocalizedException)
        {
            throw;
        }
        catch (InvalidDataException)
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.ImportInvalid);
        }
        catch (IOException)
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.ImportInvalid);
        }
        finally
        {
            DeleteStagingDirectory(staging);
        }
    }

    private async Task ExtractZipAsync(
        string staging,
        Stream zipStream,
        bool normalizeWrapper,
        bool enforceLimits,
        bool seedMainFile,
        CancellationToken cancellationToken)
    {
        using var archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: true);
        var files = new List<(ZipArchiveEntry Entry, string Path)>();

        foreach (var entry in archive.Entries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var relative = NormalizePath(entry.FullName);
            if (relative.Length == 0 || entry.FullName.EndsWith("/", StringComparison.Ordinal)
                || entry.FullName.EndsWith("\\", StringComparison.Ordinal))
            {
                continue;
            }

            files.Add((entry, relative));
        }

        var wrapper = normalizeWrapper ? DetectWrapperDirectory(files.Select(file => file.Path)) : null;
        var normalizedPaths = new HashSet<string>(StringComparer.Ordinal);
        var normalizedEntries = new List<(ZipArchiveEntry Entry, string Path)>();

        foreach (var (entry, originalPath) in files)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var path = wrapper is not null && originalPath.StartsWith(wrapper, StringComparison.Ordinal)
                ? originalPath[wrapper.Length..]
                : originalPath;
            if (path.Length == 0 || (enforceLimits && !normalizedPaths.Add(path)))
            {
                throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.ImportInvalid);
            }

            normalizedEntries.Add((entry, path));
        }

        if (enforceLimits)
        {
            ValidateNoConflictingPaths(normalizedEntries.Select(item => item.Path));
        }
        long packageBytes = 0;
        foreach (var (entry, path) in normalizedEntries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var destination = Path.GetFullPath(Path.Combine(staging, path.Replace('/', Path.DirectorySeparatorChar)));
            if (!IsWithinDirectory(staging, destination))
            {
                throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.FilePathInvalid);
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            await CopyEntryAsync(entry, destination, path, packageBytes, enforceLimits, cancellationToken);
            packageBytes += new FileInfo(destination).Length;
            if (enforceLimits && packageBytes > MaxPackageBytes)
            {
                throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.PackageTooLarge);
            }
        }

        if (seedMainFile && !File.Exists(Path.Combine(staging, MainFile)))
        {
            File.WriteAllText(Path.Combine(staging, MainFile), string.Empty);
        }
    }

    private static async Task CopyEntryAsync(
        ZipArchiveEntry entry,
        string destination,
        string path,
        long packageBytes,
        bool enforceLimits,
        CancellationToken cancellationToken)
    {
        if (enforceLimits && entry.Length > MaxFileBytes)
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.FileTooLarge, path);
        }

        if (enforceLimits && packageBytes > MaxPackageBytes - entry.Length)
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.PackageTooLarge);
        }

        await using var input = entry.Open();
        await using var output = new FileStream(destination, FileMode.Create, FileAccess.Write, FileShare.None);
        var buffer = new byte[64 * 1024];
        long fileBytes = 0;
        while (true)
        {
            var read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0)
            {
                break;
            }

            fileBytes += read;
            if (enforceLimits && fileBytes > MaxFileBytes)
            {
                throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.FileTooLarge, path);
            }

            if (enforceLimits && packageBytes > MaxPackageBytes - fileBytes)
            {
                throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.PackageTooLarge);
            }

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private static string? DetectWrapperDirectory(IEnumerable<string> paths)
    {
        var pathList = paths.ToList();
        if (pathList.Count == 0 || pathList.Any(path => !path.Contains('/')))
        {
            return null;
        }

        var firstSegments = pathList
            .Select(path => path[..path.IndexOf('/', StringComparison.Ordinal)])
            .Distinct(StringComparer.Ordinal)
            .ToList();
        // Without a root SKILL.md there is no reliable way to distinguish a
        // wrapper from a legitimate top-level folder such as examples. Preserve
        // that structure and seed the missing main file instead.
        var wrapper = firstSegments.Count == 1 ? firstSegments[0] + "/" : null;
        return wrapper is not null && pathList.Contains(wrapper + MainFile, StringComparer.Ordinal)
            ? wrapper
            : null;
    }

    private static void ValidateNoConflictingPaths(IEnumerable<string> paths)
    {
        var ordered = paths.OrderBy(path => path, StringComparer.Ordinal).ToList();
        for (var index = 0; index < ordered.Count - 1; index++)
        {
            if (ordered[index + 1].StartsWith(ordered[index] + "/", StringComparison.Ordinal))
            {
                throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.ImportInvalid);
            }
        }
    }

    private string CreateStagingDirectory()
    {
        var stagingRoot = Path.Combine(_root, ".staging");
        Directory.CreateDirectory(stagingRoot);
        var staging = Path.Combine(stagingRoot, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(staging);
        return staging;
    }

    private static void DeleteStagingDirectory(string? staging)
    {
        if (!string.IsNullOrEmpty(staging) && Directory.Exists(staging))
        {
            Directory.Delete(staging, recursive: true);
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

        var full = Path.GetFullPath(Path.Combine(dir, relative.Replace('/', Path.DirectorySeparatorChar)));
        if (!IsWithinDirectory(dir, full))
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

        // Reject traversal and rooted/absolute paths before normalizing separators.
        if (raw.Length == 0
            || raw.Contains('\0')
            || raw.StartsWith("/", StringComparison.Ordinal)
            || Path.IsPathRooted(raw)
            || (raw.Length >= 2 && char.IsLetter(raw[0]) && raw[1] == ':'))
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.FilePathInvalid);
        }

        var segments = raw.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.FilePathInvalid);
        }

        return string.Join('/', segments);
    }

    private static bool IsWithinDirectory(string root, string path)
    {
        var relative = Path.GetRelativePath(root, path);
        return relative != ".."
            && !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !Path.IsPathRooted(relative);
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

}
