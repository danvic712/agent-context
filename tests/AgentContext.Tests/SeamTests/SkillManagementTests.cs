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
/// Thin Skill management (T6, issue #7 / spec US21–23): create with domain +
/// version, publish-new-version keeps history, get_skill resolves the latest,
/// and delete removes every version. Primary seam against Testcontainers pgvector.
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

    private static SkillAppService Service(AgentContextDbContext db) => new(db);

    [Fact]
    public async Task Create_persists_skill_with_domain_and_version_one()
    {
        var (db, _, domainId) = await SeededAsync();

        var created = await Service(db).CreateAsync(new CreateSkillRequest(
            Domain: "dev", Slug: "coding-guide", Name: "Coding Guide",
            Description: "Repo conventions", Instructions: "# Guide\n\nFollow the standards."));

        Assert.Equal("dev", created.DomainName);
        Assert.Equal("coding-guide", created.Slug);
        Assert.Equal(1, created.Version);
        Assert.Equal("# Guide\n\nFollow the standards.", created.Instructions);

        var row = await db.Skills.AsNoTracking().SingleAsync(s => s.Id == created.Id);
        Assert.Equal(domainId, row.DomainId);
        Assert.Equal(1, row.Version);
        Assert.Equal("Coding Guide", row.Name);
    }

    [Fact]
    public async Task Create_rejects_duplicate_slug_in_the_same_domain()
    {
        var (db, _, _) = await SeededAsync();
        var service = Service(db);
        await service.CreateAsync(new CreateSkillRequest("dev", "coding-guide", "A", "d", "i"));

        var ex = await Assert.ThrowsAsync<LocalizedException>(() =>
            service.CreateAsync(new CreateSkillRequest("dev", "coding-guide", "B", "d", "i")));
        Assert.Equal(ErrorCodes.Skill.SlugExists, ex.ErrorCode);
    }

    [Fact]
    public async Task Create_rejects_invalid_slug_format()
    {
        var (db, _, _) = await SeededAsync();

        var ex = await Assert.ThrowsAsync<LocalizedException>(() =>
            Service(db).CreateAsync(new CreateSkillRequest("dev", "Coding Guide!", "A", "d", "i")));
        Assert.Equal(ErrorCodes.Skill.SlugInvalid, ex.ErrorCode);
    }

    [Fact]
    public async Task Publish_keeps_history_and_get_skill_returns_the_latest()
    {
        var (db, _, _) = await SeededAsync();
        var service = Service(db);

        var v1 = await service.CreateAsync(new CreateSkillRequest(
            "dev", "coding-guide", "Coding Guide", "v1", "instructions v1"));
        var v2 = await service.PublishAsync(v1.Id, new PublishSkillRequest(
            "Coding Guide", "v2", "instructions v2"));

        Assert.Equal(2, v2.Version);
        Assert.Equal(v1.Slug, v2.Slug);
        Assert.Equal("dev", v2.DomainName);

        // History is kept: both rows exist, get_skill returns the latest.
        var versions = await db.Skills.AsNoTracking()
            .Where(s => s.Slug == "coding-guide")
            .OrderBy(s => s.Version)
            .Select(s => s.Version)
            .ToListAsync();
        Assert.Equal([1, 2], versions);

        var latest = await service.GetBySlugAsync("dev", "coding-guide");
        Assert.Equal(2, latest.Version);
        Assert.Equal("instructions v2", latest.Instructions);
    }

    [Fact]
    public async Task GetBySlug_throws_when_skill_does_not_exist()
    {
        var (db, _, _) = await SeededAsync();

        await Assert.ThrowsAsync<LocalizedException>(() => Service(db).GetBySlugAsync("dev", "nope"));
        await Assert.ThrowsAsync<LocalizedException>(() => Service(db).GetBySlugAsync("home", "coding-guide"));
    }

    [Fact]
    public async Task List_returns_latest_version_per_slug()
    {
        var (db, _, _) = await SeededAsync();
        var service = Service(db);

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
    public async Task Delete_removes_every_version_and_get_skill_stops_resolving()
    {
        var (db, _, _) = await SeededAsync();
        var service = Service(db);

        var v1 = await service.CreateAsync(new CreateSkillRequest("dev", "coding-guide", "A", "d", "i1"));
        await service.PublishAsync(v1.Id, new PublishSkillRequest("A", "d", "i2"));

        await service.DeleteAsync(v1.Id);

        Assert.Equal(0, await db.Skills.CountAsync(s => s.Slug == "coding-guide"));
        await Assert.ThrowsAsync<LocalizedException>(() => service.GetBySlugAsync("dev", "coding-guide"));
    }

    [Fact]
    public async Task Delete_throws_for_unknown_id()
    {
        var (db, _, _) = await SeededAsync();

        await Assert.ThrowsAsync<LocalizedException>(() => Service(db).DeleteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Publish_throws_for_unknown_id()
    {
        var (db, _, _) = await SeededAsync();

        await Assert.ThrowsAsync<LocalizedException>(() =>
            Service(db).PublishAsync(Guid.NewGuid(), new PublishSkillRequest("A", "d", "i")));
    }

    [Fact]
    public async Task Unknown_domain_is_registered_on_create()
    {
        var (db, workspaceId, _) = await SeededAsync();

        var created = await Service(db).CreateAsync(new CreateSkillRequest("home", "guide", "A", "d", "i"));

        var domain = await db.Domains.AsNoTracking().SingleAsync(d => d.Name == "home");
        Assert.Equal(workspaceId, domain.WorkspaceId);
        Assert.Equal("home", domain.Name);
        Assert.Equal("home", created.DomainName);
    }
}
