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
