using AgentContext.Infrastructure;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace AgentContext.Tests.Testcontainers;

/// <summary>
/// A pgvector Postgres container for the Testcontainers-based seam tests
/// (spec §Testing Decisions: behavior is tested against a real Postgres with
/// pgvector; no database mocking).
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly IContainer _container;

    public PostgresFixture()
    {
        _container = new ContainerBuilder("pgvector/pgvector:pg17")
            .WithName($"agent-context-tests-{Guid.NewGuid():N}")
            .WithEnvironment("POSTGRES_USER", "agent_context")
            .WithEnvironment("POSTGRES_PASSWORD", "agent_context")
            .WithEnvironment("POSTGRES_DB", "agent_context")
            .WithEnvironment("PGPASSWORD", "agent_context")
            .WithPortBinding(5432, true)
            // pg_isready can pass while the image is still mid-initialization (it
            // restarts once after init), so wait on a real SELECT round-trip.
            .WithWaitStrategy(Wait.ForUnixContainer()
                .UntilCommandIsCompleted("psql -U agent_context -d agent_context -tAc \"SELECT 1\""))
            .Build();
    }

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task DisposeAsync() => await _container.DisposeAsync();

    public Task InitializeAsync()
    {
        async Task StartAsync()
        {
            await _container.StartAsync();
            var port = _container.GetMappedPublicPort(5432);
            ConnectionString =
                $"Host=127.0.0.1;Port={port};Database=agent_context;Username=agent_context;Password=agent_context";
        }

        return StartAsync();
    }

    /// <summary>Creates a DbContext pointed at this fixture's database (schema not yet applied).</summary>
    public AgentContextDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<AgentContextDbContext>()
            .UseNpgsql(ConnectionString, o => o.UseVector())
            .Options;
        return new AgentContextDbContext(options);
    }
}

/// <summary>
/// Base class giving every test its own container/database — xUnit creates a new
/// instance per test method, so InitializeAsync/DisposeAsync wrap each test.
/// </summary>
public abstract class PostgresTestBase : IAsyncLifetime
{
    protected PostgresFixture Fixture { get; } = new();

    public Task InitializeAsync() => Fixture.InitializeAsync();

    public Task DisposeAsync() => Fixture.DisposeAsync();
}
