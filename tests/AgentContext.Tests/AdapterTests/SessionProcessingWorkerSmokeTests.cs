using AgentContext.Application;
using AgentContext.Application.Sessions;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Host.Workers;
using AgentContext.Infrastructure;
using AgentContext.Tests.Testcontainers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentContext.Tests.AdapterTests;

/// <summary>
/// Thin smoke test for the worker scheduling (spec §Testing Decisions: "the
/// BackgroundService scheduling itself is not behavior-tested beyond a thin
/// smoke test"). The queue behaviour itself is covered at the service seam in
/// SessionProcessingTests; this only proves the worker's poll-and-process loop.
/// </summary>
public sealed class SessionProcessingWorkerSmokeTests : PostgresTestBase
{
    [Fact]
    public async Task Worker_marks_pending_sessions_processed()
    {
        // Seed first-run state and record a pending session.
        Guid sessionId;
        await using (var db = Fixture.CreateDbContext())
        {
            await db.Database.MigrateAsync();
            var workspace = new Workspace { Name = "W", Type = WorkspaceType.Personal };
            db.Workspaces.Add(workspace);
            await db.SaveChangesAsync();

            var service = new SaveSessionAppService(db);
            sessionId = (await service.SaveAsync(new SaveSessionRequest(
                Domain: "dev", Task: "t", Conclusion: "c", Model: "gpt-4o", TokensIn: 1, TokensOut: 1))).SessionId;
        }

        // Real DI graph, fast polling interval.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = Fixture.ConnectionString,
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationServices(configuration);
        await using var provider = services.BuildServiceProvider();

        var worker = new SessionProcessingWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<SessionProcessingWorker>>(),
            pollingInterval: TimeSpan.FromMilliseconds(100));

        await worker.StartAsync(CancellationToken.None);
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(10);
            SessionStatus? status = null;
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(200);
                await using var check = Fixture.CreateDbContext();
                status = await check.Sessions.AsNoTracking()
                    .Where(s => s.Id == sessionId)
                    .Select(s => s.Status)
                    .SingleOrDefaultAsync();
                if (status == SessionStatus.Completed)
                {
                    break;
                }
            }

            Assert.Equal(SessionStatus.Completed, status);

            await using var final = Fixture.CreateDbContext();
            var session = await final.Sessions.AsNoTracking().SingleAsync(s => s.Id == sessionId);
            Assert.NotNull(session.ProcessedAtUtc);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }
}
