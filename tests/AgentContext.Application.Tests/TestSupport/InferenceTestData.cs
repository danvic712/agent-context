using AgentContext.Application.Dtos;
using AgentContext.Domain;
using AgentContext.Domain.Entities;

namespace AgentContext.Application.Tests.TestSupport;

internal static class InferenceTestData
{
    public static readonly Guid OpenAiProviderId = Guid.Parse("f7d6f4b7-f2b1-4a01-9d50-0e5b7dd1b101");
    public static readonly Guid DeepSeekProviderId = Guid.Parse("f7d6f4b7-f2b1-4a01-9d50-0e5b7dd1b102");
    public static readonly Guid ConfigurationId = Guid.Parse("f7d6f4b7-f2b1-4a01-9d50-0e5b7dd1b103");
    public static readonly Guid ChatRouteId = Guid.Parse("f7d6f4b7-f2b1-4a01-9d50-0e5b7dd1b104");
    public static readonly Guid EmbeddingRouteId = Guid.Parse("f7d6f4b7-f2b1-4a01-9d50-0e5b7dd1b105");

    public static InferenceConfigurationInput ValidInput(
        string? openAiApiKey = "sk-openai-test",
        string? deepSeekApiKey = "sk-deepseek-test")
        => new(
            "Platform default",
            [
                new(
                    OpenAiProviderId,
                    "OpenAI",
                    "openai-compatible",
                    "https://api.openai.com/v1",
                    openAiApiKey),
                new(
                    DeepSeekProviderId,
                    "DeepSeek",
                    "openai-compatible",
                    "https://api.deepseek.com/v1",
                    deepSeekApiKey),
            ],
            [
                new(ChatRouteId, InferenceCapability.Chat, OpenAiProviderId, "gpt-4o-mini"),
                new(
                    EmbeddingRouteId,
                    InferenceCapability.Embedding,
                    DeepSeekProviderId,
                    "text-embedding-3-small"),
            ]);

    public static InferenceProvider OpenAiProvider(string apiKeySecretRef = "protected-openai")
        => new()
        {
            Id = OpenAiProviderId,
            Name = "OpenAI",
            ProviderType = "openai-compatible",
            BaseUrl = "https://api.openai.com/v1",
            ApiKeySecretRef = apiKeySecretRef,
            CreatedAtUtc = new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero),
            UpdatedAtUtc = new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero),
        };

    public static InferenceProvider DeepSeekProvider(string apiKeySecretRef = "protected-deepseek")
        => new()
        {
            Id = DeepSeekProviderId,
            Name = "DeepSeek",
            ProviderType = "openai-compatible",
            BaseUrl = "https://api.deepseek.com/v1",
            ApiKeySecretRef = apiKeySecretRef,
            CreatedAtUtc = new DateTimeOffset(2026, 8, 20, 0, 0, 1, TimeSpan.Zero),
            UpdatedAtUtc = new DateTimeOffset(2026, 8, 20, 0, 0, 1, TimeSpan.Zero),
        };

    public static InferenceConfiguration ConfiguredConfiguration()
        => new()
        {
            Id = ConfigurationId,
            Name = "Platform default",
            CreatedAtUtc = new DateTimeOffset(2026, 8, 20, 0, 0, 0, TimeSpan.Zero),
            UpdatedAtUtc = new DateTimeOffset(2026, 8, 20, 0, 0, 2, TimeSpan.Zero),
        };

    public static InferenceRoute ChatRoute()
        => new()
        {
            Id = ChatRouteId,
            InferenceConfigurationId = ConfigurationId,
            Capability = InferenceCapability.Chat,
            ProviderId = OpenAiProviderId,
            Model = "gpt-4o-mini",
        };

    public static InferenceRoute EmbeddingRoute()
        => new()
        {
            Id = EmbeddingRouteId,
            InferenceConfigurationId = ConfigurationId,
            Capability = InferenceCapability.Embedding,
            ProviderId = DeepSeekProviderId,
            Model = "text-embedding-3-small",
        };
}
