using AgentContext.Application.Hygiene;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using AgentContext.Tests.Testcontainers;
using Microsoft.EntityFrameworkCore;
using DomainEntity = AgentContext.Domain.Entities.Domain;

namespace AgentContext.Tests.SeamTests;

/// <summary>
/// Engine health view (T8, issue #9 / spec US29): queue depth, failures and
/// retry visibility read from the Postgres-as-queue sessions table.
/// </summary>
public sealed class EngineHealthTests : PostgresTestBase
{
    private async Task<AgentContextDbContext> SeedAsync(params Session[] sessions)
    {
        var db = Fixture.CreateDbContext();
        await db.Database.MigrateAsync();
        var workspace = new Workspace { Name = "W", Type = WorkspaceType.Personal };
        var domain = new DomainEntity { WorkspaceId = workspace.Id, Name = "dev", IsShared = false };
        db.Workspaces.Add(workspace);
        db.Domains.Add(domain);

        foreach (var s in sessions)
        {
            s.WorkspaceId = workspace.Id;
            s.DomainId = domain.Id;
        }

        db.Sessions.AddRange(sessions);
        await db.SaveChangesAsync();
        return db;
    }

    private static EngineHealthAppService Service(AgentContextDbContext db) => new(db);

    [Fact]
    public async Task Health_counts_pending_failed_and_retries_accurately()
    {
        await using var db = await SeedAsync(
            new Session { AgentName = "a", Task = "t", Conclusion = "c", SummaryJson = "{}", Status = SessionStatus.Pending },
            new Session { AgentName = "a", Task = "t", Conclusion = "c", SummaryJson = "{}", Status = SessionStatus.Processing },
            new Session { AgentName = "a", Task = "t", Conclusion = "c", SummaryJson = "{}", Status = SessionStatus.Failed, NextAttemptAtUtc = DateTimeOffset.UtcNow.AddMinutes(1) },
            new Session { AgentName = "a", Task = "t", Conclusion = "c", SummaryJson = "{}", Status = SessionStatus.Failed, NextAttemptAtUtc = null },
            new Session { AgentName = "a", Task = "t", Conclusion = "c", SummaryJson = "{}", Status = SessionStatus.Completed });

        var health = await Service(db).GetHealthAsync();

        Assert.Equal(1, health.QueuedSessions);
        Assert.Equal(1, health.ProcessingSessions);
        Assert.Equal(2, health.FailedSessions);
        Assert.Equal(1, health.RetryScheduledSessions);
        Assert.Equal(5, health.TotalSessions);
    }

    [Fact]
    public async Task Health_returns_zeros_on_empty_database()
    {
        await using var db = Fixture.CreateDbContext();
        await db.Database.MigrateAsync();

        var health = await Service(db).GetHealthAsync();

        Assert.Equal(0, health.QueuedSessions);
        Assert.Equal(0, health.ProcessingSessions);
        Assert.Equal(0, health.FailedSessions);
        Assert.Equal(0, health.RetryScheduledSessions);
        Assert.Equal(0, health.TotalSessions);
    }
}
