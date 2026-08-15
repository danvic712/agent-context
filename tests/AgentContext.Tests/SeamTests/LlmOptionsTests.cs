using AgentContext.Application.Learning;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace AgentContext.Tests.SeamTests;

/// <summary>
/// AC1 — the LLM endpoint (base URL + key) is configurable via settings
/// (ADR 0003: one OpenAI-compatible endpoint serves extraction + embedding in v1,
/// with the config shape leaving room for a separate embedding model later).
/// </summary>
public sealed class LlmOptionsTests
{
    [Fact]
    public void Binds_from_Llm_configuration_section()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Llm:BaseUrl"] = "http://localhost:11434/v1",
                ["Llm:ApiKey"] = "local-key",
                ["Llm:Model"] = "llama3.2",
                ["Llm:EmbeddingModel"] = "nomic-embed-text",
            })
            .Build();

        var options = configuration.GetSection(LlmOptions.SectionName).Get<LlmOptions>();

        Assert.NotNull(options);
        Assert.Equal("http://localhost:11434/v1", options!.BaseUrl);
        Assert.Equal("local-key", options.ApiKey);
        Assert.Equal("llama3.2", options.Model);
        Assert.Equal("nomic-embed-text", options.EmbeddingModel);
    }

    [Fact]
    public void Embedding_model_falls_back_to_the_extraction_model_when_omitted()
    {
        var options = new LlmOptions { BaseUrl = "http://x", ApiKey = "k", Model = "gpt-4o-mini" };

        Assert.Equal("gpt-4o-mini", options.EffectiveEmbeddingModel);
    }

    [Fact]
    public void Validation_reports_missing_required_fields()
    {
        var errors = LlmOptions.Validate(new LlmOptions());

        Assert.Contains(errors, e => e.Contains("BaseUrl", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("ApiKey", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(errors, e => e.Contains("Model", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validation_rejects_non_uri_base_url()
    {
        var errors = LlmOptions.Validate(new LlmOptions { BaseUrl = "not a uri", ApiKey = "k", Model = "m" });

        Assert.Contains(errors, e => e.Contains("BaseUrl", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validation_passes_for_complete_configuration()
    {
        var errors = LlmOptions.Validate(
            new LlmOptions { BaseUrl = "http://localhost:11434/v1", ApiKey = "k", Model = "m" });

        Assert.Empty(errors);
    }

    [Fact]
    public void Invalid_configuration_fails_options_resolution_via_di()
    {
        var services = new ServiceCollection();
        services.AddOptions<LlmOptions>()
            .Bind(new ConfigurationBuilder().AddInMemoryCollection().Build().GetSection(LlmOptions.SectionName));
        services.AddSingleton<IValidateOptions<LlmOptions>, LlmOptionsValidator>();

        using var provider = services.BuildServiceProvider();

        // Validate<TValidator> runs on every resolve; a broken Llm config must surface
        // as OptionsValidationException so the worker tick records it instead of hanging.
        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<LlmOptions>>().Value);
    }
}
