using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Pgvector.EntityFrameworkCore;

namespace AgentContext.Infrastructure;

/// <summary>
/// Design-time factory for `dotnet ef` (dual-mode Program.cs does not host a
/// web app in the shape EF tooling expects). Reads the same connection string
/// the runtime uses, from appsettings.json / environment.
/// </summary>
public sealed class AgentContextDbContextFactory : IDesignTimeDbContextFactory<AgentContextDbContext>
{
    public AgentContextDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Default is not configured. Set it in appsettings.json or via the ConnectionStrings__Default environment variable.");

        var options = new DbContextOptionsBuilder<AgentContextDbContext>()
            .UseNpgsql(connectionString, o => o.UseVector())
            .Options;

        return new AgentContextDbContext(options);
    }
}
