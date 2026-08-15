using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Pgvector.EntityFrameworkCore;

namespace AgentContext.Infrastructure;

/// <summary>
/// Single source of truth for the AgentContextDbContext options (Npgsql +
/// pgvector + connection string). Shared by the runtime DI registration
/// (AddApplicationServices) and the EF design-time factory so the wiring
/// cannot drift apart.
/// </summary>
public static class DbContextOptionsFactory
{
    public static string GetConnectionString(IConfiguration configuration)
        => configuration.GetConnectionString("Default")
           ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

    public static void Configure(DbContextOptionsBuilder options, IConfiguration configuration)
        => options.UseNpgsql(GetConnectionString(configuration), o => o.UseVector());

    public static DbContextOptions<AgentContextDbContext> Create(IConfiguration configuration)
    {
        var options = new DbContextOptionsBuilder<AgentContextDbContext>();
        Configure(options, configuration);
        return options.Options;
    }
}
