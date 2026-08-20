using AgentContext.Application;
using AgentContext.Application.Contracts;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace AgentContext.Application.Tests.Inference;

public sealed class DataProtectionPersistenceTests
{
    [Fact]
    public void Provider_key_ring_survives_a_service_provider_rebuild()
    {
        var keysDirectory = Path.Combine(Path.GetTempPath(), $"agent-context-keys-{Guid.NewGuid():N}");
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = "Host=localhost;Database=agent_context;Username=agent_context;Password=agent_context",
                ["DataProtection:KeysDirectory"] = keysDirectory,
            })
            .Build();

        try
        {
            string protectedSecret;
            using (var firstProvider = BuildServiceProvider(configuration))
            {
                var protector = firstProvider.GetRequiredService<IDataProtectionProvider>()
                    .CreateProtector("agent-context/inference-provider-api-key/v1");
                protectedSecret = protector.Protect("sk-persisted-test");
            }

            using (var secondProvider = BuildServiceProvider(configuration))
            {
                var protector = secondProvider.GetRequiredService<IInferenceSecretProtector>();

                Assert.Equal("sk-persisted-test", protector.Unprotect(protectedSecret));
            }
        }
        finally
        {
            if (Directory.Exists(keysDirectory))
            {
                Directory.Delete(keysDirectory, recursive: true);
            }
        }
    }

    private static ServiceProvider BuildServiceProvider(IConfiguration configuration)
        => new ServiceCollection()
            .AddApplicationServices(configuration)
            .BuildServiceProvider();
}
