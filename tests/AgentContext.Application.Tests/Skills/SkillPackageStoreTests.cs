using System.IO.Compression;
using System.Net;
using System.Text;
using AgentContext.Application.Localization;
using AgentContext.Application.Skills;

namespace AgentContext.Application.Tests.Skills;

public sealed class SkillPackageStoreTests
{
    [Fact]
    public async Task Create_from_zip_strips_a_single_wrapper_directory_and_preserves_binary_files()
    {
        var root = CreateTempDirectory();
        try
        {
            var store = new SkillPackageStore(root);
            var binary = new byte[] { 0, 1, 2, 255 };

            await store.CreatePackageFromZipAsync(
                "dev",
                "uploaded-guide",
                1,
                Zip(
                    ("uploaded-guide/SKILL.md", Encoding.UTF8.GetBytes("# Guide")),
                    ("uploaded-guide/examples/run.bin", binary)),
                CancellationToken.None);

            Assert.Equal(["SKILL.md", "examples/run.bin"],
                store.ListFiles("dev", "uploaded-guide", 1).Select(file => file.Path));
            Assert.Equal(Encoding.UTF8.GetBytes("# Guide"),
                store.ReadFile("dev", "uploaded-guide", 1, "SKILL.md"));
            Assert.Equal(binary,
                store.ReadFile("dev", "uploaded-guide", 1, "examples/run.bin"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Create_from_zip_seeds_skill_md_when_the_archive_omits_it()
    {
        var root = CreateTempDirectory();
        try
        {
            var store = new SkillPackageStore(root);

            await store.CreatePackageFromZipAsync(
                "dev", "without-main", 1, Zip(("examples/example.ts", Encoding.UTF8.GetBytes("export {};"))), CancellationToken.None);

            Assert.Contains(store.ListFiles("dev", "without-main", 1), file => file.Path == "SKILL.md");
            Assert.Contains(store.ListFiles("dev", "without-main", 1), file => file.Path == "examples/example.ts");
            Assert.Equal([], store.ReadFile("dev", "without-main", 1, "SKILL.md"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Create_from_zip_rejects_unsafe_paths_without_creating_the_target_package()
    {
        var root = CreateTempDirectory();
        try
        {
            var store = new SkillPackageStore(root);

            var exception = await Assert.ThrowsAsync<LocalizedException>(() => store.CreatePackageFromZipAsync(
                "dev", "unsafe", 1, Zip(("../../outside.txt", [1, 2, 3])), CancellationToken.None));

            Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
            Assert.Equal(ErrorCodes.Skill.FilePathInvalid, exception.ErrorCode);
            Assert.False(Directory.Exists(Path.Combine(root, "dev", "unsafe", "v1")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Create_from_zip_rejects_malformed_archives()
    {
        var root = CreateTempDirectory();
        try
        {
            var store = new SkillPackageStore(root);

            var exception = await Assert.ThrowsAsync<LocalizedException>(() => store.CreatePackageFromZipAsync(
                "dev", "malformed", 1, new MemoryStream([1, 2, 3]), CancellationToken.None));

            Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
            Assert.Equal(ErrorCodes.Skill.ImportInvalid, exception.ErrorCode);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Create_from_zip_rejects_an_entry_over_the_per_file_limit()
    {
        var root = CreateTempDirectory();
        try
        {
            var store = new SkillPackageStore(root);
            var oversized = new byte[10 * 1024 * 1024 + 1];

            var exception = await Assert.ThrowsAsync<LocalizedException>(() => store.CreatePackageFromZipAsync(
                "dev", "oversized-file", 1, Zip(("large.bin", oversized)), CancellationToken.None));

            Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
            Assert.Equal(ErrorCodes.Skill.FileTooLarge, exception.ErrorCode);
            Assert.False(Directory.Exists(Path.Combine(root, "dev", "oversized-file", "v1")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Create_from_zip_rejects_a_package_over_the_total_size_limit()
    {
        var root = CreateTempDirectory();
        try
        {
            var store = new SkillPackageStore(root);
            var file = new byte[10 * 1024 * 1024];

            var exception = await Assert.ThrowsAsync<LocalizedException>(() => store.CreatePackageFromZipAsync(
                "dev",
                "oversized-package",
                1,
                Zip(
                    ("one.bin", file),
                    ("two.bin", file),
                    ("three.bin", file),
                    ("four.bin", file),
                    ("five.bin", file),
                    ("six.bin", file)),
                CancellationToken.None));

            Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
            Assert.Equal(ErrorCodes.Skill.PackageTooLarge, exception.ErrorCode);
            Assert.False(Directory.Exists(Path.Combine(root, "dev", "oversized-package", "v1")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Import_into_existing_package_keeps_existing_main_file_when_archive_omits_it()
    {
        var root = CreateTempDirectory();
        try
        {
            var store = new SkillPackageStore(root);
            store.CreatePackage("dev", "existing", 1, "# Existing");

            await store.ImportZipAsync(
                "dev", "existing", 1, Zip(("existing/examples/new.ts", Encoding.UTF8.GetBytes("export {};"))), CancellationToken.None);

            Assert.Equal(Encoding.UTF8.GetBytes("# Existing"),
                store.ReadFile("dev", "existing", 1, "SKILL.md"));
            Assert.Equal(Encoding.UTF8.GetBytes("export {};"),
                store.ReadFile("dev", "existing", 1, "existing/examples/new.ts"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Create_from_zip_rejects_duplicate_normalized_entries()
    {
        var root = CreateTempDirectory();
        try
        {
            var store = new SkillPackageStore(root);

            var exception = await Assert.ThrowsAsync<LocalizedException>(() => store.CreatePackageFromZipAsync(
                "dev",
                "duplicate-entries",
                1,
                Zip(
                    ("SKILL.md", Encoding.UTF8.GetBytes("first")),
                    ("SKILL.md", Encoding.UTF8.GetBytes("second"))),
                CancellationToken.None));

            Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
            Assert.Equal(ErrorCodes.Skill.ImportInvalid, exception.ErrorCode);
            Assert.False(Directory.Exists(Path.Combine(root, "dev", "duplicate-entries", "v1")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Create_from_zip_rejects_file_and_child_path_conflicts()
    {
        var root = CreateTempDirectory();
        try
        {
            var store = new SkillPackageStore(root);

            var exception = await Assert.ThrowsAsync<LocalizedException>(() => store.CreatePackageFromZipAsync(
                "dev",
                "conflicting-paths",
                1,
                Zip(
                    ("a", [1]),
                    ("a/b", [2])),
                CancellationToken.None));

            Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
            Assert.Equal(ErrorCodes.Skill.ImportInvalid, exception.ErrorCode);
            Assert.False(Directory.Exists(Path.Combine(root, "dev", "conflicting-paths", "v1")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [Fact]
    public async Task Create_from_zip_treats_script_entries_as_data_without_executing_them()
    {
        var root = CreateTempDirectory();
        try
        {
            var store = new SkillPackageStore(root);
            var script = Encoding.UTF8.GetBytes("#!/bin/sh\ntouch should-not-exist\n");

            await store.CreatePackageFromZipAsync(
                "dev", "script-data", 1, Zip(("scripts/setup.sh", script)), CancellationToken.None);

            Assert.Equal(script, store.ReadFile("dev", "script-data", 1, "scripts/setup.sh"));
            Assert.False(File.Exists(Path.Combine(root, "should-not-exist")));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static MemoryStream Zip(params (string Path, byte[] Content)[] files)
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            foreach (var (path, content) in files)
            {
                using var entry = archive.CreateEntry(path).Open();
                entry.Write(content);
            }
        }

        stream.Position = 0;
        return stream;
    }

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"agent-context-skill-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }
}
