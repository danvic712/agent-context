using System.Net;
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
/// Secondary seam — analytics over REST (T7, issue #8): boots the real host
/// against Testcontainers Postgres, seeds sessions + usage + pricing, and calls
/// the overview and pricing endpoints through the web adapter.
/// </summary>
public sealed class AnalyticsApiTests : PostgresTestBase
{
    private async Task<(WebApplicationFactory<Program> Factory, HttpClient Client)> SeededAsync(
        params Session[] sessions)
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

        if (sessions.Length > 0)
        {
            await using var db = Fixture.CreateDbContext();
            var workspace = await db.Workspaces.AsNoTracking().SingleAsync();
            var dev = new DomainEntity { WorkspaceId = workspace.Id, Name = "dev", IsShared = false };
            db.Domains.Add(dev);
            await db.SaveChangesAsync();

            foreach (var session in sessions)
            {
                session.WorkspaceId = workspace.Id;
                session.DomainId = dev.Id;
            }

            db.Sessions.AddRange(sessions);
            await db.SaveChangesAsync();
        }

        return (factory, factory.CreateClient());
    }

    private static Session SessionWith(string agent, string model, int tokensIn, int tokensOut)
    {
        var session = new Session
        {
            AgentName = agent, Task = "t", Conclusion = "c", SummaryJson = "{}", Status = SessionStatus.Completed,
        };
        session.Usage.Add(new Usage { Model = model, TokensIn = tokensIn, TokensOut = tokensOut, CreatedAtUtc = DateTimeOffset.UtcNow });
        return session;
    }

    [Fact]
    public async Task Overview_returns_totals_and_breakdowns_through_rest()
    {
        var (_, client) = await SeededAsync(
            SessionWith("agent-a", "gpt-4o", 1000, 200),
            SessionWith("agent-a", "gpt-4o", 500, 100),
            SessionWith("agent-b", "claude-3.5", 2000, 300));

        var put = await client.PutAsJsonAsync("/api/analytics/pricing", new
        {
            model = "gpt-4o",
            inputCostPerToken = 0.0000025m,
            outputCostPerToken = 0.00001m,
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var overview = await client.GetFromJsonAsync<JsonElement>("/api/analytics/overview");

        Assert.Equal(3, overview.GetProperty("totalSessions").GetInt32());
        Assert.Equal(3500, overview.GetProperty("totalTokensIn").GetInt32());
        Assert.Equal(600, overview.GetProperty("totalTokensOut").GetInt32());
        Assert.Equal(0.00675m, overview.GetProperty("totalCost").GetDecimal(), 10);

        var dev = Assert.Single(overview.GetProperty("byDomain").EnumerateArray(), g => g.GetProperty("name").GetString() == "dev");
        Assert.Equal(3, dev.GetProperty("sessions").GetInt32());

        var agentA = Assert.Single(overview.GetProperty("byAgent").EnumerateArray(), g => g.GetProperty("name").GetString() == "agent-a");
        Assert.Equal(0.00675m, agentA.GetProperty("cost").GetDecimal(), 10);
    }

    [Fact]
    public async Task Overview_filters_by_agent_through_rest()
    {
        var (_, client) = await SeededAsync(
            SessionWith("agent-a", "gpt-4o", 1000, 200),
            SessionWith("agent-b", "gpt-4o", 500, 100));

        var overview = await client.GetFromJsonAsync<JsonElement>("/api/analytics/overview?agent=agent-a");

        Assert.Equal(1, overview.GetProperty("totalSessions").GetInt32());
        Assert.Equal(1000, overview.GetProperty("totalTokensIn").GetInt32());
    }

    [Fact]
    public async Task Pricing_upsert_list_delete_through_rest()
    {
        var (_, client) = await SeededAsync();

        var put = await client.PutAsJsonAsync("/api/analytics/pricing", new
        {
            model = "gpt-4o",
            inputCostPerToken = 0.0000025m,
            outputCostPerToken = 0.00001m,
        });
        put.EnsureSuccessStatusCode();

        var list = await client.GetFromJsonAsync<JsonElement>("/api/analytics/pricing");
        var row = Assert.Single(list.EnumerateArray());
        Assert.Equal("gpt-4o", row.GetProperty("model").GetString());

        // Upsert updates in place.
        var second = await client.PutAsJsonAsync("/api/analytics/pricing", new
        {
            model = "gpt-4o",
            inputCostPerToken = 0.000003m,
            outputCostPerToken = 0.000012m,
        });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        var after = await client.GetFromJsonAsync<JsonElement>("/api/analytics/pricing");
        var updated = Assert.Single(after.EnumerateArray());
        Assert.Equal(0.000003m, updated.GetProperty("inputCostPerToken").GetDecimal(), 10);

        var del = await client.DeleteAsync("/api/analytics/pricing/gpt-4o");
        Assert.Equal(HttpStatusCode.NoContent, del.StatusCode);
        var empty = await client.GetFromJsonAsync<JsonElement>("/api/analytics/pricing");
        Assert.Equal(0, empty.GetArrayLength());
    }

    [Fact]
    public async Task Pricing_rejects_invalid_input_through_rest()
    {
        var (_, client) = await SeededAsync();

        var response = await client.PutAsJsonAsync("/api/analytics/pricing", new
        {
            model = " ",
            inputCostPerToken = 1m,
            outputCostPerToken = 1m,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
