using AgentContext.Application.Learning;
using AgentContext.Application.Localization;

namespace AgentContext.Tests.SeamTests;

/// <summary>
/// Shape and validation of the LLM endpoint options (ADR 0003: one
/// OpenAI-compatible endpoint serves extraction + embedding in v1, with room
/// for a separate embedding model later). Stored in the settings table — the
/// DB read/write itself is covered by SettingsAppServiceTests.
/// </summary>
public sealed class LlmOptionsTests
{
    [Fact]
    public void Embedding_model_falls_back_to_the_extraction_model_when_omitted()
    {
        var options = new LlmOptions { BaseUrl = "http://x", ApiKey = "k", Model = "gpt-4o-mini" };

        Assert.Equal("gpt-4o-mini", options.EffectiveEmbeddingModel);
    }

    [Fact]
    public void Dedicated_embedding_model_wins_over_the_fallback()
    {
        var options = new LlmOptions { BaseUrl = "http://x", ApiKey = "k", Model = "gpt-4o-mini", EmbeddingModel = "nomic-embed-text" };

        Assert.Equal("nomic-embed-text", options.EffectiveEmbeddingModel);
    }

    [Fact]
    public void Validation_reports_missing_required_fields()
    {
        var errors = LlmOptions.Validate(new LlmOptions());

        Assert.Contains(ErrorCodes.Llm.BaseUrlRequired, errors);
        Assert.Contains(ErrorCodes.Llm.ApiKeyRequired, errors);
        Assert.Contains(ErrorCodes.Llm.ModelRequired, errors);
    }

    [Fact]
    public void Validation_rejects_non_uri_base_url()
    {
        var errors = LlmOptions.Validate(new LlmOptions { BaseUrl = "not a uri", ApiKey = "k", Model = "m" });

        Assert.Contains(ErrorCodes.Llm.BaseUrlInvalid, errors);
    }

    [Fact]
    public void Validation_passes_for_complete_configuration()
    {
        var errors = LlmOptions.Validate(
            new LlmOptions { BaseUrl = "http://localhost:11434/v1", ApiKey = "k", Model = "m" });

        Assert.Empty(errors);
    }
}
