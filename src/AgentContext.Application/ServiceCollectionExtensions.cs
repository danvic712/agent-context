using AgentContext.Application.Analytics;
using AgentContext.Application.Contracts;
using AgentContext.Application.Hygiene;
using AgentContext.Application.Inference;
using AgentContext.Application.KnowledgeManagement;
using AgentContext.Application.Learning;
using AgentContext.Application.Localization;
using AgentContext.Application.Pricing;
using AgentContext.Application.Retrieval;
using AgentContext.Application.Sessions;
using AgentContext.Application.Settings;
using AgentContext.Application.Skills;
using AgentContext.Application.Setup;
using AgentContext.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentContext.Application;

/// <summary>
/// The single shared service registration for both entrypoints (ADR 0006):
/// one DI graph, one DbContext, one configuration.
/// both call this before adding their own surface-specific wiring.
/// </summary>
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AgentContextDbContext>(options =>
            DbContextOptionsFactory.Configure(options, configuration));

        // Localization (T11): embedded i18n JSON, shared with the frontend (ADR 0008).
        services.AddSingleton<ITranslationService, TranslationService>();

        // First-run wizard
        services.AddScoped<ISetupAppService, SetupAppService>();

        // Platform inference configuration (three-table model). Provider API keys
        // are encrypted with a stable, persisted ASP.NET Core data-protection key
        // ring. The container deployment overrides the directory to a mounted
        // volume; local development uses the user's persistent application-data
        // directory instead of the build output directory.
        var dataProtectionKeysDirectory = configuration["DataProtection:KeysDirectory"];
        if (string.IsNullOrWhiteSpace(dataProtectionKeysDirectory))
        {
            var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            dataProtectionKeysDirectory = string.IsNullOrWhiteSpace(localApplicationData)
                ? Path.Combine(AppContext.BaseDirectory, "data-protection-keys")
                : Path.Combine(localApplicationData, "agent-context", "data-protection-keys");
        }

        services.AddDataProtection()
            .SetApplicationName("agent-context")
            .PersistKeysToFileSystem(new DirectoryInfo(dataProtectionKeysDirectory));
        services.AddHttpClient("inference-validation", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(10);
        });
        services.AddScoped<IInferenceSecretProtector, InferenceSecretProtector>();
        services.AddScoped<IInferenceConfigurationAppService, InferenceConfigurationAppService>();

        // Session recording (T2)
        services.AddScoped<ISaveSessionAppService, SaveSessionAppService>();

        // Learning Engine (T3, issue #4): inference routes are stored in the
        // dedicated three-table configuration and resolved per call.
        services.AddScoped<ISettingsAppService, SettingsAppService>();
        services.AddScoped<ILlmClient, LlmClient>();
        services.AddScoped<ILearningUsageRecorder, LearningUsageRecorder>();
        services.AddScoped<ILearningPipelineAppService, LearningPipelineAppService>();
        services.AddScoped<IRetrievalAppService, RetrievalAppService>();
        services.AddScoped<IKnowledgeAppService, KnowledgeAppService>();

        // Skill management (T6, issue #7): CRUD + versions + get_skill.
        // T12: the skill package (files) lives on the filesystem under Skills:Directory
        // (default ./skills), while the DB keeps metadata. Singletons are stateless
        // over the resolved root directory.
        services.AddSingleton<ISkillPackageStore>(_ => new SkillPackageStore(
            configuration["Skills:Directory"] ?? "skills"));
        services.AddScoped<ISkillAppService, SkillAppService>();

        // Session overview analytics + model pricing (T7, issue #8)
        services.AddScoped<IPricingAppService, PricingAppService>();
        services.AddScoped<IAnalyticsAppService, AnalyticsAppService>();

        // Knowledge hygiene + engine health (T8, issue #9)
        services.AddScoped<IKnowledgeHygieneAppService, KnowledgeHygieneAppService>();
        services.AddScoped<IEngineHealthAppService, EngineHealthAppService>();

        return services;
    }
}
