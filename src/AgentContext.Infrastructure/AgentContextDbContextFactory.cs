using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace AgentContext.Infrastructure;

/// <summary>
/// Design-time factory for `dotnet ef` (dual-mode Program.cs does not host a
/// web app in the shape EF tooling expects). Reads the same connection string
/// the runtime uses, from appsettings.json / environment — via the shared
/// <see cref="DbContextOptionsFactory"/>.
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

        return new AgentContextDbContext(DbContextOptionsFactory.Create(configuration));
    }
}
