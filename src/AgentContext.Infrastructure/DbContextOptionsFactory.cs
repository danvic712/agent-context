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
    /// <summary>
    /// Configuration for design-time tooling (dotnet ef), built from the same
    /// sources the runtime host uses (appsettings.json, environment-specific
    /// appsettings, environment variables) so the two paths cannot drift.
    /// </summary>
    public static IConfigurationRoot BuildDesignTimeConfiguration(string basePath)
        => new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

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
