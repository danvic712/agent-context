using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentContext.Infrastructure;
using AgentContext.Tests.Testcontainers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace AgentContext.Tests.AdapterTests;

/// <summary>
/// Secondary seam — Skill management over REST (T6, issue #7): boots the real
/// host against Testcontainers Postgres, runs the first-run wizard, and exercises
/// the CRUD + publish + get_skill endpoints through the web adapter.
/// </summary>
public sealed class SkillApiTests : PostgresTestBase
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

    [Fact]
    public async Task Create_persists_with_domain_and_version_through_rest()
    {
        var (_, client) = await SeededAsync();

        var created = await client.PostAsJsonAsync("/api/skills", new
        {
            domain = "dev",
            slug = "coding-guide",
            name = "Coding Guide",
            description = "Repo conventions",
            instructions = "# Guide",
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("dev", body.GetProperty("domainName").GetString());
        Assert.Equal("coding-guide", body.GetProperty("slug").GetString());
        Assert.Equal(1, body.GetProperty("version").GetInt32());
        Assert.Equal("# Guide", body.GetProperty("instructions").GetString());
    }

    [Fact]
    public async Task Create_rejects_duplicate_slug_through_rest()
    {
        var (_, client) = await SeededAsync();
        var payload = new
        {
            domain = "dev",
            slug = "coding-guide",
            name = "A",
            description = "d",
            instructions = "i",
        };

        (await client.PostAsJsonAsync("/api/skills", payload)).EnsureSuccessStatusCode();
        var second = await client.PostAsJsonAsync("/api/skills", payload);

        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task List_returns_latest_version_per_slug_through_rest()
    {
        var (_, client) = await SeededAsync();
        var first = await CreateAsync(client, "guide-a");
        await client.PostAsJsonAsync($"/api/skills/{first}/publish", new { name = "A", description = "d", instructions = "i2" });

        var list = await client.GetFromJsonAsync<JsonElement>("/api/skills");

        var item = Assert.Single(list.EnumerateArray(), i => i.GetProperty("slug").GetString() == "guide-a");
        Assert.Equal(2, item.GetProperty("version").GetInt32());
        Assert.Equal("dev", item.GetProperty("domainName").GetString());
    }

    [Fact]
    public async Task Publish_keeps_history_and_get_skill_returns_latest_through_rest()
    {
        var (_, client) = await SeededAsync();
        var v1Id = await CreateAsync(client, "coding-guide");

        var published = await client.PostAsJsonAsync($"/api/skills/{v1Id}/publish", new
        {
            name = "Coding Guide v2",
            description = "updated",
            instructions = "# Guide v2",
        });
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        var v2 = await published.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, v2.GetProperty("version").GetInt32());

        // get_skill over REST returns the latest version (AC3).
        var resolved = await client.GetFromJsonAsync<JsonElement>("/api/skills/by-slug?domain=dev&slug=coding-guide");
        Assert.Equal(2, resolved.GetProperty("version").GetInt32());
        Assert.Equal("# Guide v2", resolved.GetProperty("instructions").GetString());

        // History is kept in the database.
        await using var db = Fixture.CreateDbContext();
        var versions = await db.Skills.AsNoTracking()
            .Where(s => s.Slug == "coding-guide")
            .OrderBy(s => s.Version)
            .Select(s => s.Version)
            .ToListAsync();
        Assert.Equal([1, 2], versions);
    }

    [Fact]
    public async Task Get_by_slug_returns_404_for_unknown_skill()
    {
        var (_, client) = await SeededAsync();

        var response = await client.GetAsync("/api/skills/by-slug?domain=dev&slug=nope");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_removes_every_version_through_rest()
    {
        var (_, client) = await SeededAsync();
        var id = await CreateAsync(client, "coding-guide");
        await client.PostAsJsonAsync($"/api/skills/{id}/publish", new { name = "A", description = "d", instructions = "i2" });

        var response = await client.DeleteAsync($"/api/skills/{id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var after = await client.GetAsync("/api/skills/by-slug?domain=dev&slug=coding-guide");
        Assert.Equal(HttpStatusCode.NotFound, after.StatusCode);
        await using var db = Fixture.CreateDbContext();
        Assert.Equal(0, await db.Skills.CountAsync(s => s.Slug == "coding-guide"));
    }

    private static async Task<Guid> CreateAsync(HttpClient client, string slug)
    {
        var response = await client.PostAsJsonAsync("/api/skills", new
        {
            domain = "dev",
            slug,
            name = "Skill",
            description = "d",
            instructions = "i",
        });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
    }
}
