using AgentContext.Application.Contracts;
using AgentContext.Application.Learning;
using AgentContext.Application.Sessions;
using AgentContext.Application.Setup;
using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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

        // Session recording (T2)
        services.AddScoped<ISaveSessionAppService, SaveSessionAppService>();

        // Learning Engine (T3, issue #4): configured OpenAI-compatible LLM endpoint
        // (ADR 0003) + the pipeline. LlmOptionsValidator runs on every resolve, so an
        // invalid/missing Llm config fails where the pipeline uses it (the worker
        // tick) without blocking platform startup/setup. The worker schedules via
        // ILearningPipelineAppService.ProcessNextAsync; tests drive ProcessAsync directly.
        services.AddOptions<LlmOptions>().Bind(configuration.GetSection(LlmOptions.SectionName));
        services.AddSingleton<IValidateOptions<LlmOptions>, LlmOptionsValidator>();
        services.AddSingleton<ILlmClient, LlmClient>();
        services.AddScoped<ILearningPipelineAppService, LearningPipelineAppService>();

        return services;
    }
}
