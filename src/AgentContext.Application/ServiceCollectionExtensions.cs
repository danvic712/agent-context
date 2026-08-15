using AgentContext.Application.Contracts;
using AgentContext.Application.Sessions;
using AgentContext.Application.Setup;
using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentContext.Application;

/// <summary>
/// The single shared service registration for both entrypoints (ADR 0006):
/// one DI graph, one DbContext, one configuration. `--web` and `--mcp-stdio`
/// both call this before adding their own surface-specific wiring.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AgentContextDbContext>(options =>
            DbContextOptionsFactory.Configure(options, configuration));

        // First-run wizard
        services.AddScoped<ISetupAppService, SetupAppService>();

        // Session recording + Postgres-as-queue processing (T2)
        services.AddScoped<ISaveSessionAppService, SaveSessionAppService>();
        services.AddScoped<ISessionProcessingAppService, SessionProcessingAppService>();

        return services;
    }
}
