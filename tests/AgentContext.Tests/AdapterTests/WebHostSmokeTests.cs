using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentContext.Tests.Testcontainers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace AgentContext.Tests.AdapterTests;

/// <summary>
/// Secondary seam — adapter contract tests for the web mode (spec §Testing
/// Decisions: REST via WebApplicationFactory). Boots the real Program.cs against
/// a Testcontainers Postgres; startup applies migrations, then the API and the
/// built React UI must be reachable.
/// </summary>
public sealed class WebHostSmokeTests : PostgresTestBase
{
    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.UseSetting("ConnectionStrings:Default", Fixture.ConnectionString));

    [Fact]
    public async Task Web_mode_serves_health_api()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ok", body.GetProperty("status").GetString());
        Assert.Equal("ok", body.GetProperty("database").GetString());
    }

    [Fact]
    public async Task Fresh_database_reports_unconfigured_to_the_wizard()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/setup");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(body.GetProperty("configured").GetBoolean());
    }

    [Fact]
    public async Task Setup_endpoint_creates_and_then_blocks_rerun()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var create = await client.PostAsJsonAsync("/api/setup", new
        {
            displayName = "Ada",
            email = "ada@example.com",
            password = "correct-horse-battery",
        });
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var created = await create.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Ada's Workspace", created.GetProperty("workspaceName").GetString());

        // The wizard now reports configured.
        var status = await client.GetFromJsonAsync<JsonElement>("/api/setup");
        Assert.True(status.GetProperty("configured").GetBoolean());

        // Rerunning is blocked with 409.
        var rerun = await client.PostAsJsonAsync("/api/setup", new
        {
            displayName = "Grace",
            email = "grace@example.com",
            password = "correct-horse-battery",
        });
        Assert.Equal(HttpStatusCode.Conflict, rerun.StatusCode);
    }

    [Fact]
    public async Task Web_mode_serves_the_react_ui()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("<div id=\"root\">", html);
        Assert.Contains("Agent Context", html);
    }
}
