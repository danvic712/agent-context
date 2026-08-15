using Microsoft.EntityFrameworkCore.Design;

namespace AgentContext.Infrastructure;

/// <summary>
/// Design-time factory for `dotnet ef` (dual-mode Program.cs does not host a
/// web app in the shape EF tooling expects). Uses the shared
/// <see cref="DbContextOptionsFactory"/> for both the configuration sources
/// and the Npgsql+pgvector options.
/// </summary>
public sealed class AgentContextDbContextFactory : IDesignTimeDbContextFactory<AgentContextDbContext>
{
    public AgentContextDbContext CreateDbContext(string[] args)
    {
        var configuration = DbContextOptionsFactory.BuildDesignTimeConfiguration(Directory.GetCurrentDirectory());
        return new AgentContextDbContext(DbContextOptionsFactory.Create(configuration));
    }
}
