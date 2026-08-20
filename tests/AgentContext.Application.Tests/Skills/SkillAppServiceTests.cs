using System.Net;
using AgentContext.Application.Contracts;
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
