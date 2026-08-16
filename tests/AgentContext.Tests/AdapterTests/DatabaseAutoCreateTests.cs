using System.Net.Http.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AgentContext.Tests.AdapterTests;

/// <summary>
/// Startup behaviour: when the database does not exist, the host creates it and
/// applies EF Core migrations automatically — no manual <c>createdb</c> step.
/// The container initializes a *different* database (seed_db), and the
/// connection string points at agent_context, which does not exist yet.
/// </summary>
public sealed class DatabaseAutoCreateTests : IAsyncLifetime
{
    private readonly IContainer _container = new ContainerBuilder("pgvector/pgvector:pg17")
        .WithName($"agent-context-autocreate-{Guid.NewGuid():N}")
        .WithEnvironment("POSTGRES_USER", "agent_context")
        .WithEnvironment("POSTGRES_PASSWORD", "agent_context")
        .WithEnvironment("POSTGRES_DB", "seed_db") // deliberately NOT the target database
        .WithEnvironment("PGPASSWORD", "agent_context")
        .WithPortBinding(5432, true)
        .WithWaitStrategy(Wait.ForUnixContainer()
            .UntilCommandIsCompleted("psql -U agent_context -d seed_db -tAc \"SELECT 1\""))
        .Build();

    private string _connectionString = string.Empty;

    public Task InitializeAsync() => StartAsync();

    public async Task DisposeAsync() => await _container.DisposeAsync();

    private async Task StartAsync()
    {
        await _container.StartAsync();
        var port = _container.GetMappedPublicPort(5432);
        _connectionString =
            $"Host=127.0.0.1;Port={port};Database=agent_context;Username=agent_context;Password=agent_context";
    }

    [Fact]
    public async Task Startup_creates_the_database_and_applies_migrations_when_missing()
    {
        using var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder => builder.UseSetting("ConnectionStrings:Default", _connectionString));

        // Booting the host runs the startup block: create DB (if missing) → migrate.
        using var client = factory.CreateClient();

        var health = await client.GetFromJsonAsync<HealthStatus>("api/health");

        Assert.NotNull(health);
        Assert.Equal("ok", health!.Status);
        Assert.Equal("ok", health.Database);
    }

    private sealed record HealthStatus(string Status, string Database);
}
