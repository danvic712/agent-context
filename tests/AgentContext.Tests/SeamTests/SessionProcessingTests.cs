using AgentContext.Application.Sessions;
using AgentContext.Application.Contracts;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using AgentContext.Tests.Testcontainers;
using Microsoft.EntityFrameworkCore;

namespace AgentContext.Tests.SeamTests;

/// <summary>
/// Primary seam — Postgres-as-queue processing (ADR 0005 / T2 AC: the
/// BackgroundService marks processed sessions done, crash-safe because the queue
/// state lives in the database). The worker itself is only a scheduler; the
/// behaviour is tested here at the application service seam.
/// </summary>
public sealed class SessionProcessingTests : PostgresTestBase
{
    private async Task<AgentContextDbContext> SeededAsync()
    {
        var db = Fixture.CreateDbContext();
        await db.Database.MigrateAsync();
        var workspace = new Workspace { Name = "W", Type = WorkspaceType.Personal };
        db.Workspaces.Add(workspace);
        await db.SaveChangesAsync();

        db.Sessions.AddRange(
            new Session { WorkspaceId = workspace.Id, AgentName = "a", Task = "1", Conclusion = "c" },
            new Session { WorkspaceId = workspace.Id, AgentName = "a", Task = "2", Conclusion = "c", Status = SessionStatus.Completed },
            new Session { WorkspaceId = workspace.Id, AgentName = "a", Task = "3", Conclusion = "c" });
        await db.SaveChangesAsync();

        return db;
    }

    [Fact]
    public async Task MarkProcessed_completes_only_pending_sessions()
    {
        var db = await SeededAsync();
        var processor = new SessionProcessingAppService(db);

        var processed = await processor.MarkProcessedAsync();

        Assert.Equal(2, processed);
        // AsNoTracking: ExecuteUpdateAsync writes via SQL and does not update
        // tracked entities — read the database state, not the change tracker.
        var states = await db.Sessions.AsNoTracking().Select(s => new { s.Task, s.Status }).ToListAsync();
        Assert.All(states, s => Assert.Equal(SessionStatus.Completed, s.Status));
        Assert.All(await db.Sessions.AsNoTracking().Where(s => s.Status == SessionStatus.Completed && s.Task != "2").ToListAsync(),
            s => Assert.NotNull(s.ProcessedAtUtc));

        // Idempotent on a second run: nothing left pending.
        Assert.Equal(0, await processor.MarkProcessedAsync());
    }
}
