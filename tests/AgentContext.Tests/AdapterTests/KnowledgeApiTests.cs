using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentContext.Application.Contracts;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using AgentContext.Tests.Fakes;
using AgentContext.Tests.Testcontainers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Pgvector;
using DomainEntity = AgentContext.Domain.Entities.Domain;

namespace AgentContext.Tests.AdapterTests;

/// <summary>
/// Secondary seam — retrieval over REST (issue #5 AC1/AC2): boots the real host
/// against Testcontainers Postgres, seeds Knowledge with embeddings, and calls
/// the endpoints through the web adapter. The LLM is faked at the DI boundary.
/// </summary>
public sealed class KnowledgeApiTests : PostgresTestBase
{
    private async Task<(WebApplicationFactory<Program> Factory, HttpClient Client)> SeededAsync(
        FakeLlmClient? llm = null, params Knowledge[] knowledge)
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:Default", Fixture.ConnectionString);
                builder.ConfigureTestServices(services =>
                    services.AddSingleton<ILlmClient>(llm ?? new FakeLlmClient()));
            });

        using var setupClient = factory.CreateClient();
        var setup = await setupClient.PostAsJsonAsync("/api/setup", new
        {
            displayName = "Danvic",
            email = "danvic@example.com",
            password = "correct-horse-battery",
        });
        setup.EnsureSuccessStatusCode();

        // Seed Knowledge directly against the fixture DB (retrieval is read-only in T4).
        if (knowledge.Length > 0)
        {
            await using var db = Fixture.CreateDbContext();
            var workspace = await db.Workspaces.AsNoTracking().SingleAsync();
            var domain = new DomainEntity { WorkspaceId = workspace.Id, Name = "dev", IsShared = false };
            db.Domains.Add(domain);
            foreach (var item in knowledge)
            {
                item.WorkspaceId = workspace.Id;
                item.DomainId = domain.Id;
            }

            db.Knowledge.AddRange(knowledge);
            await db.SaveChangesAsync();
        }

        return (factory, factory.CreateClient());
    }

    [Fact]
    public async Task List_returns_knowledge_with_confidence_through_rest()
    {
        var (_, client) = await SeededAsync(
            llm: null,
            Item("alpha", KnowledgeType.Solution, confidence: 0.8),
            Item("beta", KnowledgeType.Pattern, confidence: 0.3));

        var list = await client.GetFromJsonAsync<JsonElement>("/api/knowledge");

        Assert.Equal(2, list.GetArrayLength());
        var alpha = list.EnumerateArray().Single(i => i.GetProperty("content").GetString() == "alpha");
        Assert.Equal("Solution", alpha.GetProperty("type").GetString());
        Assert.True(alpha.GetProperty("confidence").GetDouble() > 0);
        Assert.Equal("dev", alpha.GetProperty("domainName").GetString());
    }

    [Fact]
    public async Task Review_returns_only_below_threshold_items_through_rest()
    {
        var (_, client) = await SeededAsync(
            llm: null,
            Item("alpha", KnowledgeType.Solution, confidence: 0.8),
            Item("beta", KnowledgeType.Pattern, confidence: 0.3));

        var review = await client.GetFromJsonAsync<JsonElement>("/api/knowledge/review");

        Assert.True(review.GetProperty("threshold").GetDouble() == 0.5);
        var item = Assert.Single(review.GetProperty("items").EnumerateArray());
        Assert.Equal("beta", item.GetProperty("content").GetString());
        Assert.True(item.GetProperty("confidence").GetDouble() < review.GetProperty("threshold").GetDouble());
    }

    [Fact]
    public async Task Patch_toggles_the_private_marker_through_rest()
    {
        var (_, client) = await SeededAsync(llm: null, Item("alpha"));
        var alphaId = await KnowledgeIdAsync(client, "alpha");

        var response = await client.PatchAsJsonAsync($"/api/knowledge/{alphaId}", new { isPrivate = true });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var after = await client.GetFromJsonAsync<JsonElement>("/api/knowledge");
        Assert.True(after[0].GetProperty("isPrivate").GetBoolean());
    }

    [Fact]
    public async Task Delete_removes_the_item_through_rest()
    {
        var (_, client) = await SeededAsync(llm: null, Item("alpha"), Item("beta"));
        var alphaId = await KnowledgeIdAsync(client, "alpha");

        var response = await client.DeleteAsync($"/api/knowledge/{alphaId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        var after = await client.GetFromJsonAsync<JsonElement>("/api/knowledge");
        Assert.Equal(1, after.GetArrayLength());
        Assert.Equal("beta", after[0].GetProperty("content").GetString());
    }

    [Fact]
    public async Task Rate_bumps_and_clears_confidence_through_rest()
    {
        var (_, client) = await SeededAsync(llm: null, Item("alpha", confidence: 0.6));
        var alphaId = await KnowledgeIdAsync(client, "alpha");

        var useful = await client.PostAsJsonAsync($"/api/knowledge/{alphaId}/rate", new { useful = true });
        useful.EnsureSuccessStatusCode();
        var bumped = (await useful.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("confidence").GetDouble();
        Assert.Equal(0.7, bumped, 3);

        var notUseful = await client.PostAsJsonAsync($"/api/knowledge/{alphaId}/rate", new { useful = false });
        var cleared = (await notUseful.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("confidence").GetDouble();
        Assert.Equal(0, cleared);
    }

    [Fact]
    public async Task Unknown_id_returns_404()
    {
        var (_, client) = await SeededAsync();

        var response = await client.DeleteAsync($"/api/knowledge/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static async Task<Guid> KnowledgeIdAsync(HttpClient client, string content)
    {
        var list = await client.GetFromJsonAsync<JsonElement>("/api/knowledge");
        return list.EnumerateArray().Single(i => i.GetProperty("content").GetString() == content)
            .GetProperty("id").GetGuid();
    }

    private static Knowledge Item(string content, KnowledgeType type = KnowledgeType.Solution, double confidence = 0.8)
        => new()
        {
            Type = type,
            Title = $"Title {content}",
            Content = content,
            Confidence = confidence,
            Embedding = new Vector(FakeLlmClient.VectorFor(content)),
            Status = KnowledgeStatus.Active,
        };

    [Fact]
    public async Task Search_returns_domain_scoped_knowledge_through_rest()
    {
        var (_, client) = await SeededAsync(llm: null, Item("alpha"), Item("beta"));

        var response = await client.GetFromJsonAsync<JsonElement>("/api/knowledge/search?domain=dev&q=alpha");

        var items = response.GetProperty("items");
        Assert.NotEqual(0, items.GetArrayLength());
        Assert.Equal("alpha", items[0].GetProperty("content").GetString());
        Assert.True(items[0].GetProperty("score").GetDouble() > 0.9);
    }

    [Fact]
    public async Task Similar_solution_returns_the_best_solution_through_rest()
    {
        var (_, client) = await SeededAsync(
            llm: null,
            Item("alpha", KnowledgeType.Solution),
            Item("alpha", KnowledgeType.Problem)); // same content, wrong type — must not win

        var response = await client.GetFromJsonAsync<JsonElement>(
            "/api/knowledge/similar-solution?domain=dev&problem=alpha");

        var solution = response.GetProperty("solution");
        Assert.Equal("Solution", solution.GetProperty("type").GetString());
        Assert.Equal("alpha", solution.GetProperty("content").GetString());
    }
}
