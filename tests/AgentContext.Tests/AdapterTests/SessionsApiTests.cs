using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentContext.Tests.Testcontainers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace AgentContext.Tests.AdapterTests;

/// <summary>
/// Secondary seam — save_session over REST (T2 AC: "Calling save_session via REST
/// creates a Session row with Usage attached"). Boots the real host against a
/// Testcontainers Postgres; the wizard runs first to create the workspace.
/// </summary>
public sealed class SessionsApiTests : PostgresTestBase
{
    private async Task<WebApplicationFactory<Program>> SeededFactoryAsync()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.UseSetting("ConnectionStrings:Default", Fixture.ConnectionString));

        using var client = factory.CreateClient();
        var setup = await client.PostAsJsonAsync("/api/setup", new
        {
            displayName = "Danvic",
            email = "danvic@example.com",
            password = "correct-horse-battery",
        });
        setup.EnsureSuccessStatusCode();

        return factory;
    }

    [Fact]
    public async Task Post_session_creates_row_with_usage()
    {
        using var factory = await SeededFactoryAsync();
        using var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/sessions", new
        {
            domain = "dev",
            task = "Fix pgvector index",
            conclusion = "hnsw works",
            keySnippets = new[] { "vector_l2_ops" },
            agentName = "craft-agents",
            model = "gpt-4o",
            tokensIn = 1200,
            tokensOut = 800,
            cost = 0.42m,
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var sessionId = body.GetProperty("sessionId").GetGuid();

        // Read the database directly through the fixture's connection string.
        await using var check = Fixture.CreateDbContext();
        var session = await check.Sessions.Include(s => s.Domain).Include(s => s.Usage).SingleAsync(s => s.Id == sessionId);
        Assert.Equal("dev", session.Domain!.Name);
        Assert.Equal("craft-agents", session.AgentName);

        var usage = Assert.Single(session.Usage);
        Assert.Equal("gpt-4o", usage.Model);
        Assert.Equal(1200, usage.TokensIn);
        Assert.Equal(800, usage.TokensOut);
        Assert.Equal(0.42m, usage.Cost);
    }

    [Fact]
    public async Task Get_and_list_expose_usage_rollups()
    {
        using var factory = await SeededFactoryAsync();
        using var client = factory.CreateClient();

        var created = await client.PostAsJsonAsync("/api/sessions", new
        {
            domain = "dev",
            task = "t",
            conclusion = "c",
            model = "gpt-4o",
            tokensIn = 1000,
            tokensOut = 500,
            cost = 0.1m,
        });
        created.EnsureSuccessStatusCode();
        var sessionId = (await created.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("sessionId").GetGuid();

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/sessions/{sessionId}");
        var usage = detail.GetProperty("usage")[0];
        Assert.Equal(1500, usage.GetProperty("tokensIn").GetInt32() + usage.GetProperty("tokensOut").GetInt32());

        var list = await client.GetFromJsonAsync<JsonElement>("/api/sessions");
        var item = list[0];
        Assert.Equal(1500, item.GetProperty("totalTokens").GetInt32());
        Assert.Equal(0.1m, item.GetProperty("totalCost").GetDecimal());
    }
}
