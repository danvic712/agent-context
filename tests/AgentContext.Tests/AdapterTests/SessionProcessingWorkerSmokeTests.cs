using AgentContext.Application;
using AgentContext.Application.Contracts;
using AgentContext.Application.Pricing;
using AgentContext.Application.Dtos;
using AgentContext.Application.Learning;
using AgentContext.Application.Sessions;
using AgentContext.Application.Settings;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Host.Workers;
using AgentContext.Infrastructure;
using AgentContext.Tests.Fakes;
using AgentContext.Tests.Testcontainers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using DomainEntity = AgentContext.Domain.Entities.Domain;

namespace AgentContext.Tests.AdapterTests;

/// <summary>
/// Thin smoke test for the worker scheduling (spec §Testing Decisions: "the
/// BackgroundService scheduling itself is not behavior-tested beyond a thin
/// smoke test"). AC5 — the pipeline runs identically when invoked directly
/// through the seam (LearningPipelineTests) and via the BackgroundService:
/// here a real DI graph with a faked ILlmClient drives the full pipeline from
/// a recorded pending Session to Knowledge in pgvector.
/// </summary>
public sealed class SessionProcessingWorkerSmokeTests : PostgresTestBase
{
    [Fact]
    public async Task Worker_runs_the_learning_pipeline_on_pending_sessions()
    {
        // Seed first-run state and record a pending session in a domain.
        Guid sessionId;
        await using (var db = Fixture.CreateDbContext())
        {
            await db.Database.MigrateAsync();
            var workspace = new Workspace { Name = "W", Type = WorkspaceType.Personal };
            var domain = new DomainEntity { WorkspaceId = workspace.Id, Name = "dev", IsShared = false };
            db.Workspaces.Add(workspace);
            db.Domains.Add(domain);
            await db.SaveChangesAsync();

            var service = new SaveSessionAppService(db, new PricingAppService(db));
            sessionId = (await service.SaveAsync(new SaveSessionRequest(
                Domain: "dev", Task: "t", Conclusion: "c", Model: "gpt-4o", TokensIn: 1, TokensOut: 1))).SessionId;

            // LLM endpoint config lives in the settings table (DB-backed settings).
            await new SettingsAppService(db).SaveLlmOptionsAsync(new LlmOptions
            {
                BaseUrl = "http://localhost:11434/v1",
                ApiKey = "test-key",
                Model = "llama3.2",
            });
        }

        // Real DI graph (shared AddApplicationServices), fast polling interval,
        // LLM faked at the seam — never the database.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = Fixture.ConnectionString,
            })
            .Build();

        var fake = new FakeLlmClient();
        fake.EnqueueExtractions(new KnowledgeExtraction(
            KnowledgeType.Solution, "Fix DB timeouts",
            "A concrete fix: configure the connection string with a thirty second timeout and retry with " +
            "exponential backoff so transient database outages never surface to callers.", 0.8));

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddApplicationServices(configuration);
        services.AddSingleton<ILlmClient>(fake); // overrides the real LlmClient registration
        await using var provider = services.BuildServiceProvider();

        var worker = new SessionProcessingWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            provider.GetRequiredService<ILogger<SessionProcessingWorker>>(),
            pollingInterval: TimeSpan.FromMilliseconds(100));

        await worker.StartAsync(CancellationToken.None);
        try
        {
            var deadline = DateTime.UtcNow.AddSeconds(15);
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

            var knowledge = await final.Knowledge.AsNoTracking().SingleAsync();
            Assert.Equal(KnowledgeType.Solution, knowledge.Type);
            Assert.Equal(0.52, knowledge.Confidence, 3);
            Assert.NotNull(knowledge.Embedding);
        }
        finally
        {
            await worker.StopAsync(CancellationToken.None);
        }
    }
}
