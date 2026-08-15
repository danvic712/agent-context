using AgentContext.Application.Setup;
using AgentContext.Application.Contracts;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using AgentContext.Tests.Testcontainers;
using Microsoft.EntityFrameworkCore;

namespace AgentContext.Tests.SeamTests;

/// <summary>
/// Primary seam — first-run wizard behavior at the application service boundary
/// against a real Postgres (spec §Testing Decisions).
/// </summary>
public sealed class SetupWizardTests : PostgresTestBase
{
    private async Task<AgentContextDbContext> MigratedContextAsync()
    {
        var db = Fixture.CreateDbContext();
        await db.Database.MigrateAsync();
        return db;
    }

    [Fact]
    public async Task Fresh_platform_is_not_configured()
    {
        await using var db = await MigratedContextAsync();
        var service = new SetupAppService(db);

        var status = await service.GetStatusAsync();

        Assert.False(status.Configured);
    }

    [Fact]
    public async Task Configure_creates_admin_user_and_personal_workspace()
    {
        await using var db = await MigratedContextAsync();
        var service = new SetupAppService(db);

        var result = await service.ConfigureAsync(
            new SetupRequest("Danvic", "danvic@example.com", "correct-horse-battery"));

        Assert.NotEqual(Guid.Empty, result.UserId);
        Assert.NotEqual(Guid.Empty, result.WorkspaceId);
        Assert.Equal("Danvic's Workspace", result.WorkspaceName);

        var user = await db.Users.SingleAsync(u => u.Id == result.UserId);
        Assert.Equal("danvic@example.com", user.Email);
        Assert.StartsWith("pbkdf2-sha256$", user.PasswordHash);

        var workspace = await db.Workspaces.SingleAsync(w => w.Id == result.WorkspaceId);
        Assert.Equal(WorkspaceType.Personal, workspace.Type);

        var membership = await db.Memberships.SingleAsync(m => m.WorkspaceId == result.WorkspaceId);
        Assert.Equal(result.UserId, membership.UserId);
        Assert.Equal(MembershipRole.Admin, membership.Role);
    }

    [Fact]
    public async Task Configure_rejects_invalid_input()
    {
        await using var db = await MigratedContextAsync();
        var service = new SetupAppService(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ConfigureAsync(new SetupRequest("", "danvic@example.com", "password123")));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ConfigureAsync(new SetupRequest("Danvic", "not-an-email", "password123")));

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.ConfigureAsync(new SetupRequest("Danvic", "danvic@example.com", "short")));
    }

    [Fact]
    public async Task Rerunning_wizard_is_blocked_once_configured()
    {
        await using var db = await MigratedContextAsync();
        var service = new SetupAppService(db);

        await service.ConfigureAsync(new SetupRequest("Danvic", "danvic@example.com", "correct-horse-battery"));
        var status = await service.GetStatusAsync();

        Assert.True(status.Configured);

        await Assert.ThrowsAsync<SetupAlreadyConfiguredException>(() =>
            service.ConfigureAsync(new SetupRequest("Other", "other@example.com", "correct-horse-battery")));

        // Nothing was written by the blocked rerun.
        Assert.Equal(1, await db.Users.CountAsync());
        Assert.Equal(1, await db.Workspaces.CountAsync());
    }
}
