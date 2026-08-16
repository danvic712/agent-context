using System.Text;
using AgentContext.Application.Localization;
using AgentContext.Application.Skills;

namespace AgentContext.Tests.SeamTests;

/// <summary>
/// Filesystem Skill package store (T12): pure unit tests — no database. Covers
/// file CRUD, binary detection, the 10 MB cap, path-traversal rejection and zip
/// import safety.
/// </summary>
public sealed class SkillPackageStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "agent-context-store-" + Guid.NewGuid().ToString("N"));
    private readonly SkillPackageStore _store;

    public SkillPackageStoreTests()
    {
        _store = new SkillPackageStore(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void EnsurePackage_creates_skilL_md_from_fallback_and_is_idempotent()
    {
        var first = _store.EnsurePackage("dev", "guide", 1, "# initial");
        Assert.True(File.Exists(Path.Combine(first, "SKILL.md")));

        // Idempotent: an existing SKILL.md is never overwritten by a fallback.
        _store.EnsurePackage("dev", "guide", 1, "# changed");
        Assert.Equal("# initial", File.ReadAllText(Path.Combine(first, "SKILL.md")));
    }

    [Fact]
    public void Write_and_read_files_round_trip_text_and_binary()
    {
        _store.EnsurePackage("dev", "guide", 1);
        _store.WriteFile("dev", "guide", 1, "examples/a.ts", Encoding.UTF8.GetBytes("const a = 1"));
        _store.WriteFile("dev", "guide", 1, "assets/blob.bin", [0x00, 0x01, 0xFF]);

        var files = _store.ListFiles("dev", "guide", 1);
        Assert.Equal(["SKILL.md", "assets/blob.bin", "examples/a.ts"], files.Select(f => f.Path).ToArray());
        Assert.True(files.Single(f => f.Path == "assets/blob.bin").Binary);
        Assert.False(files.Single(f => f.Path == "examples/a.ts").Binary);

        Assert.Equal([0x00, 0x01, 0xFF], _store.ReadFile("dev", "guide", 1, "assets/blob.bin"));
        Assert.Equal("const a = 1", Encoding.UTF8.GetString(_store.ReadFile("dev", "guide", 1, "examples/a.ts")));
    }

    [Theory]
    [InlineData("../escape.md")]
    [InlineData("a/../../escape.md")]
    [InlineData("/abs/path.md")]
    [InlineData("")]
    public void Path_traversal_is_rejected(string path)
    {
        _store.EnsurePackage("dev", "guide", 1);

        var ex = Assert.Throws<LocalizedException>(() =>
            _store.WriteFile("dev", "guide", 1, path, [1]));
        Assert.Equal(ErrorCodes.Skill.FilePathInvalid, ex.ErrorCode);
    }

    [Fact]
    public void Oversized_files_are_rejected()
    {
        _store.EnsurePackage("dev", "guide", 1);

        var ex = Assert.Throws<LocalizedException>(() =>
            _store.WriteFile("dev", "guide", 1, "big.bin", new byte[10 * 1024 * 1024 + 1]));
        Assert.Equal(ErrorCodes.Skill.FileTooLarge, ex.ErrorCode);
    }

    [Fact]
    public void Missing_files_are_reported_as_not_found()
    {
        _store.EnsurePackage("dev", "guide", 1);

        var ex = Assert.Throws<LocalizedException>(() => _store.ReadFile("dev", "guide", 1, "nope.md"));
        Assert.Equal(ErrorCodes.Skill.FileNotFound, ex.ErrorCode);
    }

    [Fact]
    public void Zip_import_rejects_traversal_entries()
    {
        _store.EnsurePackage("dev", "guide", 1);
        using var zip = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(zip, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            using (var writer = new StreamWriter(archive.CreateEntry("../evil.txt").Open()))
            {
                writer.Write("bad");
            }
        }
        zip.Position = 0;

        var ex = Assert.Throws<LocalizedException>(() => _store.ImportZip("dev", "guide", 1, zip));
        Assert.Equal(ErrorCodes.Skill.FilePathInvalid, ex.ErrorCode);
    }

    [Fact]
    public void Delete_package_removes_the_whole_directory()
    {
        _store.EnsurePackage("dev", "guide", 1);
        _store.WriteFile("dev", "guide", 1, "extra.txt", [1]);

        _store.DeletePackage("dev", "guide", 1);

        Assert.False(Directory.Exists(Path.Combine(_root, "dev", "guide", "v1")));
    }
}
