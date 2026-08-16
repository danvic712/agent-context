using System.Text;
using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Application.Localization;
using AgentContext.Application.Skills;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using AgentContext.Tests.Testcontainers;
using Microsoft.EntityFrameworkCore;
using DomainEntity = AgentContext.Domain.Entities.Domain;

namespace AgentContext.Tests.SeamTests;

/// <summary>
/// Skill management (T6, issue #7 / spec US21–23 + T12 package model): create with
/// domain + version, publish-new-version keeps history, get_skill resolves the
/// latest, per-file operations and zip import against the filesystem package, and
/// delete removes every version (rows + package directories). Primary seam against
/// Testcontainers pgvector with a per-test temp directory for packages.
/// </summary>
public sealed class SkillManagementTests : PostgresTestBase
{
    private async Task<(AgentContextDbContext Db, Guid WorkspaceId, Guid DomainId)> SeededAsync()
    {
        var db = Fixture.CreateDbContext();
        await db.Database.MigrateAsync();
        var workspace = new Workspace { Name = "W", Type = WorkspaceType.Personal };
        var domain = new DomainEntity { WorkspaceId = workspace.Id, Name = "dev", IsShared = false };
        db.Workspaces.Add(workspace);
        db.Domains.Add(domain);
        await db.SaveChangesAsync();
        return (db, workspace.Id, domain.Id);
    }

    private static (SkillAppService Service, SkillPackageStore Store) Service(AgentContextDbContext db)
    {
        var root = Path.Combine(Path.GetTempPath(), "agent-context-skills-" + Guid.NewGuid().ToString("N"));
        var store = new SkillPackageStore(root);
        return (new SkillAppService(db, store), store);
    }

    [Fact]
    public async Task Create_seeds_the_package_with_skilL_md_from_instructions()
    {
        var (db, _, domainId) = await SeededAsync();
        var (service, store) = Service(db);

        var created = await service.CreateAsync(new CreateSkillRequest(
            Domain: "dev", Slug: "coding-guide", Name: "Coding Guide",
            Description: "Repo conventions", Instructions: "# Guide\n\nFollow the standards."));

        Assert.Equal("dev", created.DomainName);
        Assert.Equal("coding-guide", created.Slug);
        Assert.Equal(1, created.Version);
        // The package manifest lists SKILL.md; the legacy Instructions landed there.
        var main = Assert.Single(created.Manifest, f => f.Path == "SKILL.md");
        Assert.Equal("# Guide\n\nFollow the standards.", Encoding.UTF8.GetString(
            store.ReadFile("dev", "coding-guide", 1, "SKILL.md")));

        var row = await db.Skills.AsNoTracking().SingleAsync(s => s.Id == created.Id);
        Assert.Equal(domainId, row.DomainId);
        Assert.Equal(1, row.Version);
        Assert.Equal("Coding Guide", row.Name);
        // The legacy Instructions column is kept in sync as the migration buffer.
        Assert.Equal("# Guide\n\nFollow the standards.", row.Instructions);
    }

    [Fact]
    public async Task Create_rejects_duplicate_slug_in_the_same_domain()
    {
        var (db, _, _) = await SeededAsync();
        var (service, _) = Service(db);
        await service.CreateAsync(new CreateSkillRequest("dev", "coding-guide", "A", "d", "i"));

        var ex = await Assert.ThrowsAsync<LocalizedException>(() =>
            service.CreateAsync(new CreateSkillRequest("dev", "coding-guide", "B", "d", "i")));
        Assert.Equal(ErrorCodes.Skill.SlugExists, ex.ErrorCode);
    }

    [Fact]
    public async Task Create_rejects_invalid_slug_format()
    {
        var (db, _, _) = await SeededAsync();
        var (service, _) = Service(db);

        var ex = await Assert.ThrowsAsync<LocalizedException>(() =>
            service.CreateAsync(new CreateSkillRequest("dev", "Coding Guide!", "A", "d", "i")));
        Assert.Equal(ErrorCodes.Skill.SlugInvalid, ex.ErrorCode);
    }

    [Fact]
    public async Task Publish_keeps_history_and_get_skill_returns_the_latest_package()
    {
        var (db, _, _) = await SeededAsync();
        var (service, store) = Service(db);

        var v1 = await service.CreateAsync(new CreateSkillRequest(
            "dev", "coding-guide", "Coding Guide", "v1", "instructions v1"));
        var v2 = await service.PublishAsync(v1.Id, new PublishSkillRequest(
            "Coding Guide", "v2", "instructions v2"));

        Assert.Equal(2, v2.Version);
        Assert.Equal(v1.Slug, v2.Slug);
        Assert.Equal("dev", v2.DomainName);

        // History is kept: both rows exist and both package versions exist.
        var versions = await db.Skills.AsNoTracking()
            .Where(s => s.Slug == "coding-guide")
            .OrderBy(s => s.Version)
            .Select(s => s.Version)
            .ToListAsync();
        Assert.Equal([1, 2], versions);
        Assert.True(Directory.Exists(Path.Combine(store.RootDirectory, "dev", "coding-guide", "v1")));
        Assert.True(Directory.Exists(Path.Combine(store.RootDirectory, "dev", "coding-guide", "v2")));

        // Each version keeps its own SKILL.md.
        Assert.Equal("instructions v1", Encoding.UTF8.GetString(store.ReadFile("dev", "coding-guide", 1, "SKILL.md")));
        Assert.Equal("instructions v2", Encoding.UTF8.GetString(store.ReadFile("dev", "coding-guide", 2, "SKILL.md")));

        var latest = await service.GetBySlugAsync("dev", "coding-guide");
        Assert.Equal(2, latest.Version);
        var main = Assert.Single(latest.Manifest, f => f.Path == "SKILL.md");
        Assert.Equal("instructions v2", Encoding.UTF8.GetString(
            store.ReadFile("dev", "coding-guide", 2, main.Path)));
    }

    [Fact]
    public async Task GetBySlug_throws_when_skill_does_not_exist()
    {
        var (db, _, _) = await SeededAsync();
        var (service, _) = Service(db);

        await Assert.ThrowsAsync<LocalizedException>(() => service.GetBySlugAsync("dev", "nope"));
        await Assert.ThrowsAsync<LocalizedException>(() => service.GetBySlugAsync("home", "coding-guide"));
    }

    [Fact]
    public async Task List_returns_latest_version_per_slug()
    {
        var (db, _, _) = await SeededAsync();
        var (service, _) = Service(db);

        var a1 = await service.CreateAsync(new CreateSkillRequest("dev", "guide-a", "A", "d", "i1"));
        await service.PublishAsync(a1.Id, new PublishSkillRequest("A", "d", "i2"));
        await service.CreateAsync(new CreateSkillRequest("dev", "guide-b", "B", "d", "i"));

        var items = await service.ListAsync();

        Assert.Equal(2, items.Count);
        var a = Assert.Single(items, i => i.Slug == "guide-a");
        Assert.Equal(2, a.Version);
        Assert.Equal("dev", a.DomainName);
        var b = Assert.Single(items, i => i.Slug == "guide-b");
        Assert.Equal(1, b.Version);
    }

    [Fact]
    public async Task Delete_removes_every_version_rows_and_package_directories()
    {
        var (db, _, _) = await SeededAsync();
        var (service, store) = Service(db);

        var v1 = await service.CreateAsync(new CreateSkillRequest("dev", "coding-guide", "A", "d", "i1"));
        await service.PublishAsync(v1.Id, new PublishSkillRequest("A", "d", "i2"));

        await service.DeleteAsync(v1.Id);

        Assert.Equal(0, await db.Skills.CountAsync(s => s.Slug == "coding-guide"));
        Assert.False(Directory.Exists(Path.Combine(store.RootDirectory, "dev", "coding-guide")));
        await Assert.ThrowsAsync<LocalizedException>(() => service.GetBySlugAsync("dev", "coding-guide"));
    }

    [Fact]
    public async Task Delete_throws_for_unknown_id()
    {
        var (db, _, _) = await SeededAsync();
        var (service, _) = Service(db);

        await Assert.ThrowsAsync<LocalizedException>(() => service.DeleteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Publish_throws_for_unknown_id()
    {
        var (db, _, _) = await SeededAsync();
        var (service, _) = Service(db);

        await Assert.ThrowsAsync<LocalizedException>(() =>
            service.PublishAsync(Guid.NewGuid(), new PublishSkillRequest("A", "d", "i")));
    }

    [Fact]
    public async Task Unknown_domain_is_registered_on_create()
    {
        var (db, workspaceId, _) = await SeededAsync();
        var (service, _) = Service(db);

        var created = await service.CreateAsync(new CreateSkillRequest("home", "guide", "A", "d", "i"));

        var domain = await db.Domains.AsNoTracking().SingleAsync(d => d.Name == "home");
        Assert.Equal(workspaceId, domain.WorkspaceId);
        Assert.Equal("home", domain.Name);
        Assert.Equal("home", created.DomainName);
    }

    [Fact]
    public async Task Existing_skill_without_a_package_migrates_lazily_from_instructions()
    {
        var (db, _, _) = await SeededAsync();
        // Seed a skill row directly (pre-package shape): Instructions in the DB,
        // no filesystem package yet.
        var workspace = await db.Workspaces.AsNoTracking().SingleAsync();
        var domain = await db.Domains.AsNoTracking().SingleAsync(d => d.Name == "dev");
        db.Skills.Add(new Skill
        {
            WorkspaceId = workspace.Id,
            DomainId = domain.Id,
            Slug = "legacy",
            Name = "Legacy",
            Description = "d",
            Instructions = "# Legacy skill",
            Version = 1,
        });
        await db.SaveChangesAsync();

        var (service, store) = Service(db);

        var detail = await service.GetBySlugAsync("dev", "legacy");
        var main = Assert.Single(detail.Manifest, f => f.Path == "SKILL.md");
        Assert.Equal("# Legacy skill", Encoding.UTF8.GetString(
            store.ReadFile("dev", "legacy", 1, main.Path)));
    }

    [Fact]
    public async Task File_operations_create_edit_and_delete_package_files()
    {
        var (db, _, _) = await SeededAsync();
        var (service, store) = Service(db);

        var created = await service.CreateAsync(new CreateSkillRequest("dev", "toolbox", "A", "d", "# A"));

        // Write a text file into a subdirectory.
        var detail = await service.WriteFileAsync(created.Id, "examples/tool.ts",
            Encoding.UTF8.GetBytes("export const tool = () => 1;"));
        Assert.Contains(detail.Manifest, f => f.Path == "examples/tool.ts");

        // Write a binary asset.
        await service.WriteFileAsync(created.Id, "assets/logo.png", [0x89, 0x50, 0x4E, 0x47, 0x00]);
        var binary = Assert.Single((await service.GetAsync(created.Id)).Manifest, f => f.Path == "assets/logo.png");
        Assert.True(binary.Binary);

        // Read back both.
        var text = Encoding.UTF8.GetString(await service.ReadFileAsync(created.Id, "examples/tool.ts"));
        Assert.Equal("export const tool = () => 1;", text);
        var png = await service.ReadFileAsync(created.Id, "assets/logo.png");
        Assert.Equal(0x89, png[0]);
        Assert.Equal(0x00, png[^1]);

        // The full package (agent consumption) carries manifest + contents.
        var pkg = await service.GetPackageAsync("dev", "toolbox");
        Assert.Contains(pkg.Manifest, f => f.Path == "examples/tool.ts");
        var binaryFile = Assert.Single(pkg.Files, f => f.Path == "assets/logo.png");
        Assert.True(binaryFile.Binary);
        Assert.Equal(Convert.ToBase64String(png), binaryFile.Content);

        // Delete prunes the file.
        var after = await service.DeleteFileAsync(created.Id, "examples/tool.ts");
        Assert.DoesNotContain(after.Manifest, f => f.Path == "examples/tool.ts");
        await Assert.ThrowsAsync<LocalizedException>(() => service.ReadFileAsync(created.Id, "examples/tool.ts"));
    }

    [Fact]
    public async Task Zip_import_extracts_files_into_the_package()
    {
        var (db, _, _) = await SeededAsync();
        var (service, store) = Service(db);

        var created = await service.CreateAsync(new CreateSkillRequest("dev", "kit", "A", "d", "# original"));

        using var zip = new MemoryStream();
        using (var archive = new System.IO.Compression.ZipArchive(zip, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            using (var writer = new StreamWriter(archive.CreateEntry("SKILL.md").Open()))
            {
                writer.Write("# Imported");
            }
            using (var toolWriter = new StreamWriter(archive.CreateEntry("tools/run.sh").Open()))
            {
                toolWriter.Write("#!/bin/sh\necho hi");
            }
        }
        zip.Position = 0;

        var detail = await service.ImportZipAsync(created.Id, zip);

        Assert.Contains(detail.Manifest, f => f.Path == "tools/run.sh");
        Assert.Equal("# Imported", Encoding.UTF8.GetString(store.ReadFile("dev", "kit", 1, "SKILL.md")));
        Assert.Equal("#!/bin/sh\necho hi", Encoding.UTF8.GetString(store.ReadFile("dev", "kit", 1, "tools/run.sh")));
    }
}
