using System.Net;
using System.Text;
using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Application.Localization;
using AgentContext.Application.Skills;
using AgentContext.Application.Tests.TestSupport;
using AgentContext.Domain.Entities;
using Moq;
using DomainEntity = AgentContext.Domain.Entities.Domain;

namespace AgentContext.Application.Tests.Skills;

public sealed class SkillAppServiceTests
{
    [Fact]
    public async Task Create_starts_a_manual_skill_at_version_one_and_seeds_the_main_file()
    {
        var workspace = Workspace("workspace");
        var domain = CreateDomain("dev", workspace.Id);
        var packages = new Mock<ISkillPackageStore>();
        packages.Setup(store => store.EnsurePackage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>()))
            .Returns(string.Empty);
        packages.Setup(store => store.ListFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .Returns([new SkillFileInfo("SKILL.md", 12, false)]);
        var context = MockSkillDbContext.Create([workspace], [domain]);
        var service = new SkillAppService(context.Object, packages.Object);

        var result = await service.CreateAsync(new CreateSkillRequest(
            "dev", "manual-guide", "Manual Guide", "A manual skill", "# Instructions"));

        Assert.Equal(1, result.Version);
        Assert.Equal("manual", result.SourceType);
        Assert.Equal("SKILL.md", Assert.Single(result.Manifest).Path);
        packages.Verify(store => store.CreatePackage("dev", "manual-guide", 1, "# Instructions"), Times.Once);
    }

    [Fact]
    public async Task Create_from_zip_creates_a_zip_skill_and_uses_the_imported_main_file()
    {
        var workspace = Workspace("workspace");
        var domain = CreateDomain("dev", workspace.Id);
        var packages = new Mock<ISkillPackageStore>();
        packages.Setup(store => store.CreatePackageFromZipAsync(
                "dev", "uploaded-guide", 1, It.IsAny<Stream>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        packages.Setup(store => store.ReadFile("dev", "uploaded-guide", 1, "SKILL.md"))
            .Returns(Encoding.UTF8.GetBytes("# Uploaded"));
        packages.Setup(store => store.EnsurePackage(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>()))
            .Returns(string.Empty);
        packages.Setup(store => store.ListFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>()))
            .Returns([new SkillFileInfo("SKILL.md", 10, false)]);
        var context = MockSkillDbContext.Create([workspace], [domain]);
        var service = new SkillAppService(context.Object, packages.Object);

        var result = await service.CreateFromZipAsync(
            new CreateSkillFromZipRequest("dev", "uploaded-guide", "Uploaded Guide", "Imported package"),
            new MemoryStream([1, 2, 3]));

        Assert.Equal(1, result.Version);
        Assert.Equal("zip", result.SourceType);
        packages.Verify(store => store.CreatePackageFromZipAsync(
            "dev", "uploaded-guide", 1, It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Once);
        packages.Verify(store => store.ReadFile("dev", "uploaded-guide", 1, "SKILL.md"), Times.Once);
    }

    [Fact]
    public async Task Create_from_zip_honors_cancellation_before_resolving_the_domain()
    {
        var workspace = Workspace("workspace");
        var packages = new Mock<ISkillPackageStore>();
        var context = MockSkillDbContext.Create([workspace]);
        var service = new SkillAppService(context.Object, packages.Object);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.CreateFromZipAsync(
            new CreateSkillFromZipRequest("dev", "uploaded-guide", "Uploaded Guide", "Imported package"),
            new MemoryStream(),
            cancellation.Token));

        packages.Verify(store => store.CreatePackageFromZipAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_from_zip_rejects_a_slug_that_already_exists_without_writing_a_package()
    {
        var workspace = Workspace("workspace");
        var domain = CreateDomain("dev", workspace.Id);
        var existing = Skill("uploaded-guide", 1, domain, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var packages = new Mock<ISkillPackageStore>();
        var service = new SkillAppService(
            MockSkillDbContext.Create([workspace], [domain], [existing]).Object,
            packages.Object);

        var exception = await Assert.ThrowsAsync<LocalizedException>(() => service.CreateFromZipAsync(
            new CreateSkillFromZipRequest("dev", "uploaded-guide", "Uploaded Guide", "Duplicate"),
            new MemoryStream()));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal(ErrorCodes.Skill.SlugExists, exception.ErrorCode);
        packages.Verify(store => store.CreatePackageFromZipAsync(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<Stream>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Create_rejects_a_slug_that_already_exists_without_writing_a_package()
    {
        var workspace = Workspace("workspace");
        var domain = CreateDomain("dev", workspace.Id);
        var existing = Skill("manual-guide", 1, domain, Guid.NewGuid(), DateTimeOffset.UtcNow);
        var packages = new Mock<ISkillPackageStore>();
        var service = new SkillAppService(
            MockSkillDbContext.Create([workspace], [domain], [existing]).Object,
            packages.Object);

        var exception = await Assert.ThrowsAsync<LocalizedException>(() => service.CreateAsync(
            new CreateSkillRequest("dev", "manual-guide", "Manual Guide", "Duplicate", "# Instructions")));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal(ErrorCodes.Skill.SlugExists, exception.ErrorCode);
        packages.Verify(store => store.CreatePackage(
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string?>()), Times.Never);
    }

    [Fact]
    public async Task List_returns_latest_version_only_and_first_page_in_stable_order()
    {
        var workspace = Workspace("workspace");
        var domain = CreateDomain("dev", workspace.Id);
        var updatedAt = new DateTimeOffset(2026, 8, 21, 10, 0, 0, TimeSpan.Zero);
        var latestAlphaId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var skills = new[]
        {
            Skill("alpha", 1, domain, Guid.Parse("00000000-0000-0000-0000-000000000001"), updatedAt.AddMinutes(5)),
            Skill("alpha", 2, domain, latestAlphaId, updatedAt.AddMinutes(1)),
            Skill("beta", 1, domain, Guid.Parse("00000000-0000-0000-0000-000000000003"), updatedAt.AddMinutes(5)),
            Skill("gamma", 1, domain, Guid.Parse("00000000-0000-0000-0000-000000000004"), updatedAt),
        };
        var service = CreateService(workspace, domain, skills);

        var result = await service.ListAsync(2);

        Assert.Equal(2, result.PageSize);
        Assert.Null(result.Cursor);
        Assert.True(result.HasMore);
        Assert.NotNull(result.NextCursor);
        Assert.Collection(
            result.Items,
            item =>
            {
                Assert.Equal("beta", item.Slug);
                Assert.Equal(1, item.Version);
            },
            item =>
            {
                Assert.Equal("alpha", item.Slug);
                Assert.Equal(2, item.Version);
                Assert.Equal(latestAlphaId, item.Id);
            });
        Assert.DoesNotContain(result.Items, item => item.Version == 1 && item.Slug == "alpha");
    }

    [Fact]
    public async Task List_cursor_returns_next_page_without_duplicates_and_marks_final_page()
    {
        var workspace = Workspace("workspace");
        var domain = CreateDomain("dev", workspace.Id);
        var updatedAt = new DateTimeOffset(2026, 8, 21, 10, 0, 0, TimeSpan.Zero);
        var skills = Enumerable.Range(1, 5)
            .Select(index => Skill(
                $"skill-{index}",
                1,
                domain,
                Guid.Parse($"00000000-0000-0000-0000-{index:000000000000}"),
                updatedAt))
            .ToArray();
        var service = CreateService(workspace, domain, skills);

        var first = await service.ListAsync(2);
        var second = await service.ListAsync(2, first.NextCursor);
        var final = await service.ListAsync(2, second.NextCursor);

        Assert.Equal(["skill-5", "skill-4"], first.Items.Select(item => item.Slug));
        Assert.Equal(["skill-3", "skill-2"], second.Items.Select(item => item.Slug));
        Assert.Equal(["skill-1"], final.Items.Select(item => item.Slug));
        Assert.True(first.HasMore);
        Assert.True(second.HasMore);
        Assert.False(final.HasMore);
        Assert.Null(final.NextCursor);
        Assert.Equal(first.Items.Select(item => item.Id).Intersect(second.Items.Select(item => item.Id)), []);
        Assert.Equal(first.NextCursor, second.Cursor);
        Assert.Equal(second.NextCursor, final.Cursor);
    }

    [Fact]
    public async Task List_groups_latest_versions_by_domain_and_slug()
    {
        var workspace = Workspace("workspace");
        var dev = CreateDomain("dev", workspace.Id);
        var home = CreateDomain("home", workspace.Id);
        var updatedAt = new DateTimeOffset(2026, 8, 21, 10, 0, 0, TimeSpan.Zero);
        var service = CreateService(
            workspace,
            dev,
            [
                Skill("shared", 1, dev, Guid.NewGuid(), updatedAt),
                Skill("shared", 2, dev, Guid.NewGuid(), updatedAt.AddMinutes(1)),
                Skill("shared", 1, home, Guid.NewGuid(), updatedAt.AddMinutes(2)),
            ],
            [home]);

        var result = await service.ListAsync(10);

        Assert.Equal(2, result.Items.Count);
        Assert.Contains(result.Items, item => item.DomainName == "dev" && item.Version == 2);
        Assert.Contains(result.Items, item => item.DomainName == "home" && item.Version == 1);
    }

    [Fact]
    public async Task List_applies_search_domain_and_source_filters()
    {
        var workspace = Workspace("workspace");
        var dev = CreateDomain("dev", workspace.Id);
        var home = CreateDomain("home", workspace.Id);
        var matching = Skill("release-guide", 1, dev, Guid.NewGuid(), DateTimeOffset.UtcNow);
        matching.Name = "Release Guide";
        matching.Description = "Ship a release safely";
        matching.SourceType = "zip";
        var wrongSource = Skill("release-manual", 1, dev, Guid.NewGuid(), DateTimeOffset.UtcNow);
        wrongSource.Name = "Release Manual";
        wrongSource.SourceType = "manual";
        var wrongDomain = Skill("release-home", 1, home, Guid.NewGuid(), DateTimeOffset.UtcNow);
        wrongDomain.Name = "Release Home";
        wrongDomain.SourceType = "zip";
        var service = CreateService(workspace, dev, [matching, wrongSource, wrongDomain], [home]);

        var result = await service.ListAsync(new SkillListQuery(
            PageSize: 10,
            Search: "release guide",
            Domain: "DEV",
            SourceType: "zip",
            Sort: "name-asc"));

        var item = Assert.Single(result.Items);
        Assert.Equal("release-guide", item.Slug);
        Assert.Equal("dev", item.DomainName);
        Assert.Equal("zip", item.SourceType);
    }

    [Fact]
    public async Task List_custom_sort_keeps_cursor_scoped_to_the_sort()
    {
        var workspace = Workspace("workspace");
        var domain = CreateDomain("dev", workspace.Id);
        var skills = new[]
        {
            Skill("zulu", 1, domain, Guid.Parse("00000000-0000-0000-0000-000000000003"), DateTimeOffset.UtcNow),
            Skill("alpha", 1, domain, Guid.Parse("00000000-0000-0000-0000-000000000001"), DateTimeOffset.UtcNow),
            Skill("bravo", 1, domain, Guid.Parse("00000000-0000-0000-0000-000000000002"), DateTimeOffset.UtcNow),
        };
        var service = CreateService(workspace, domain, skills);

        var first = await service.ListAsync(new SkillListQuery(PageSize: 2, Sort: "name-asc"));
        var second = await service.ListAsync(new SkillListQuery(PageSize: 2, Cursor: first.NextCursor, Sort: "name-asc"));

        Assert.Equal(["alpha", "bravo"], first.Items.Select(item => item.Slug));
        Assert.Equal(["zulu"], second.Items.Select(item => item.Slug));
        Assert.False(second.HasMore);
    }

    [Fact]
    public async Task List_rejects_an_unknown_sort()
    {
        var service = CreateService(Workspace("workspace"));

        var exception = await Assert.ThrowsAsync<LocalizedException>(() => service.ListAsync(
            new SkillListQuery(Sort: "random")));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal(ErrorCodes.Skill.SortInvalid, exception.ErrorCode);
    }

    [Fact]
    public async Task List_rejects_a_cursor_created_for_different_filters()
    {
        var workspace = Workspace("workspace");
        var domain = CreateDomain("dev", workspace.Id);
        var service = CreateService(
            workspace,
            domain,
            [
                Skill("alpha", 1, domain, Guid.NewGuid(), DateTimeOffset.UtcNow),
                Skill("bravo", 1, domain, Guid.NewGuid(), DateTimeOffset.UtcNow.AddMinutes(-1)),
            ]);

        var first = await service.ListAsync(new SkillListQuery(PageSize: 1, Sort: "name-asc"));
        var exception = await Assert.ThrowsAsync<LocalizedException>(() => service.ListAsync(
            new SkillListQuery(PageSize: 1, Cursor: first.NextCursor, Sort: "updated-desc")));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal(ErrorCodes.Skill.CursorInvalid, exception.ErrorCode);
    }

    [Fact]
    public async Task List_returns_empty_page_for_empty_library()
    {
        var workspace = Workspace("workspace");
        var service = CreateService(workspace);

        var result = await service.ListAsync();

        Assert.Equal(20, result.PageSize);
        Assert.Empty(result.Items);
        Assert.False(result.HasMore);
        Assert.Null(result.NextCursor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(101)]
    public async Task List_rejects_page_sizes_outside_the_bounded_range(int pageSize)
    {
        var service = CreateService(Workspace("workspace"));

        var exception = await Assert.ThrowsAsync<LocalizedException>(() => service.ListAsync(pageSize));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal(ErrorCodes.Skill.PageSizeInvalid, exception.ErrorCode);
    }

    [Fact]
    public async Task List_rejects_malformed_cursor_with_a_localized_error()
    {
        var service = CreateService(Workspace("workspace"));

        var exception = await Assert.ThrowsAsync<LocalizedException>(() => service.ListAsync(20, "not-a-cursor"));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal(ErrorCodes.Skill.CursorInvalid, exception.ErrorCode);
    }

    [Fact]
    public async Task List_honors_cancellation_before_querying_the_database()
    {
        var workspace = Workspace("workspace");
        var service = CreateService(workspace);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.ListAsync(20, null, cancellation.Token));
    }

    private static ISkillAppService CreateService(
        Workspace workspace,
        DomainEntity? domain = null,
        IEnumerable<Skill>? skills = null,
        IEnumerable<DomainEntity>? additionalDomains = null)
    {
        var domains = additionalDomains?.ToList() ?? [];
        if (domain is not null)
        {
            domains.Add(domain);
        }

        var context = MockSkillDbContext.Create(
            [workspace],
            domains,
            skills ?? []);
        return new SkillAppService(context.Object, Mock.Of<ISkillPackageStore>());
    }

    private static Workspace Workspace(string name)
        => new() { Id = Guid.NewGuid(), Name = name, CreatedAtUtc = DateTimeOffset.UtcNow };

    private static DomainEntity CreateDomain(string name, Guid workspaceId)
        => new() { Id = Guid.NewGuid(), WorkspaceId = workspaceId, Name = name, CreatedAtUtc = DateTimeOffset.UtcNow };

    private static Skill Skill(string slug, int version, DomainEntity domain, Guid id, DateTimeOffset updatedAt)
        => new()
        {
            Id = id,
            WorkspaceId = domain.WorkspaceId,
            DomainId = domain.Id,
            Domain = domain,
            Slug = slug,
            Name = slug,
            Description = $"{slug} description",
            Instructions = $"{slug} instructions",
            Version = version,
            CreatedAtUtc = updatedAt,
            UpdatedAtUtc = updatedAt,
        };
}
