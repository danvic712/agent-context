using System.Net.Http.Json;
using System.Text.Json;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using AgentContext.Tests.Testcontainers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using DomainEntity = AgentContext.Domain.Entities.Domain;

namespace AgentContext.Tests.AdapterTests;

/// <summary>
/// Secondary seam — engine health + hygiene over REST (T8, issue #9): boots the
/// real host, seeds sessions/knowledge, and drives the health view and the
/// on-demand hygiene run through the web adapter.
/// </summary>
public sealed class EngineHealthApiTests : PostgresTestBase
{
    private async Task<(WebApplicationFactory<Program> Factory, HttpClient Client)> SeededAsync()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.UseSetting("ConnectionStrings:Default", Fixture.ConnectionString));

        using var setupClient = factory.CreateClient();
        var setup = await setupClient.PostAsJsonAsync("/api/setup", new
        {
            displayName = "Danvic",
            email = "danvic@example.com",
            password = "correct-horse-battery",
        });
        setup.EnsureSuccessStatusCode();

        return (factory, factory.CreateClient());
    }

    private static Session Session(SessionStatus status, DateTimeOffset? nextAttempt = null) => new()
    {
        AgentName = "a", Task = "t", Conclusion = "c", SummaryJson = "{}",
        Status = status, NextAttemptAtUtc = nextAttempt,
    };

    [Fact]
    public async Task Engine_health_reports_queue_and_retries_through_rest()
    {
        var (_, client) = await SeededAsync();
        await using (var db = Fixture.CreateDbContext())
        {
            var workspace = await db.Workspaces.AsNoTracking().SingleAsync();
            var domain = new DomainEntity { WorkspaceId = workspace.Id, Name = "dev", IsShared = false };
            db.Domains.Add(domain);
            await db.SaveChangesAsync();
            var sessions = new[]
            {
                Session(SessionStatus.Pending),
                Session(SessionStatus.Processing),
                Session(SessionStatus.Failed, DateTimeOffset.UtcNow.AddMinutes(1)),
                Session(SessionStatus.Failed, null),
            };
            foreach (var s in sessions)
            {
                s.WorkspaceId = workspace.Id;
                s.DomainId = domain.Id;
            }

            db.Sessions.AddRange(sessions);
            await db.SaveChangesAsync();
        }

        var health = await client.GetFromJsonAsync<JsonElement>("/api/health/engine");

        Assert.Equal(1, health.GetProperty("queuedSessions").GetInt32());
        Assert.Equal(1, health.GetProperty("processingSessions").GetInt32());
        Assert.Equal(2, health.GetProperty("failedSessions").GetInt32());
        Assert.Equal(1, health.GetProperty("retryScheduledSessions").GetInt32());
        Assert.Equal(4, health.GetProperty("totalSessions").GetInt32());
    }

    [Fact]
    public async Task Hygiene_run_endpoint_decays_and_moves_to_review_through_rest()
    {
        var (_, client) = await SeededAsync();
        await using (var db = Fixture.CreateDbContext())
        {
            var workspace = await db.Workspaces.AsNoTracking().SingleAsync();
            var domain = new DomainEntity { WorkspaceId = workspace.Id, Name = "dev", IsShared = false };
            db.Domains.Add(domain);
            await db.SaveChangesAsync();
            db.Knowledge.Add(new Knowledge
            {
                WorkspaceId = workspace.Id, DomainId = domain.Id,
                Type = KnowledgeType.Solution, Title = "Weak", Content = "weak",
                Confidence = 0.6, Status = KnowledgeStatus.Active,
                CreatedAtUtc = DateTimeOffset.UtcNow.AddDays(-130),
                UpdatedAtUtc = DateTimeOffset.UtcNow.AddDays(-130),
            });
            await db.SaveChangesAsync();
        }

        var run = await client.PostAsync("/api/knowledge/hygiene/run", null);
        run.EnsureSuccessStatusCode();
        var result = await run.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, result.GetProperty("decayed").GetInt32());
        Assert.Equal(1, result.GetProperty("movedToReview").GetInt32());
        Assert.Equal(0, result.GetProperty("archived").GetInt32());

        // Item now shows in the review list (T8: Review is an explicit status).
        var review = await client.GetFromJsonAsync<JsonElement>("/api/knowledge/review");
        Assert.Contains(review.GetProperty("items").EnumerateArray(),
            i => i.GetProperty("content").GetString() == "weak");

        // Re-running is idempotent (AC2).
        var second = await client.PostAsync("/api/knowledge/hygiene/run", null);
        var secondResult = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, secondResult.GetProperty("decayed").GetInt32());
        Assert.Equal(0, secondResult.GetProperty("movedToReview").GetInt32());
        Assert.Equal(0, secondResult.GetProperty("archived").GetInt32());
    }
}
