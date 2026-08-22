using System.Net;
using System.Net.Http.Json;
using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Application.Learning;
using AgentContext.Domain;
using Moq;

namespace AgentContext.Application.Tests.Learning;

public sealed class LlmClientTests
{
    [Fact]
    public async Task Extract_returns_functional_result_usage_and_route_snapshot()
    {
        var chatRouteId = Guid.NewGuid();
        var client = CreateClient(
            chatRouteId,
            Guid.NewGuid(),
            (_, _) => ChatResponse(includeUsage: true));

        var result = await client.ExtractKnowledgeAsync("{}");

        Assert.Empty(result.Result);
        Assert.Equal(chatRouteId, result.InferenceRouteId);
        Assert.Equal("chat-model-snapshot", result.Model);
        Assert.Equal(new LlmUsage(100, 25, 40), result.Usage);
    }

    [Fact]
    public async Task Embed_returns_functional_result_usage_and_route_snapshot()
    {
        var embeddingRouteId = Guid.NewGuid();
        var client = CreateClient(
            Guid.NewGuid(),
            embeddingRouteId,
            (_, _) => EmbeddingResponse(includeUsage: true));

        var result = await client.EmbedAsync("knowledge text");

        Assert.Equal(LearningPipelineDefaults.EmbeddingDimensions, result.Result.Length);
        Assert.Equal(embeddingRouteId, result.InferenceRouteId);
        Assert.Equal("embedding-model-snapshot", result.Model);
        Assert.Equal(new LlmUsage(12, 0, 0), result.Usage);
    }

    [Fact]
    public async Task Missing_provider_usage_preserves_functional_result_without_fabricating_counts()
    {
        var client = CreateClient(
            Guid.NewGuid(),
            Guid.NewGuid(),
            (_, _) => ChatResponse(includeUsage: false));

        var result = await client.ExtractKnowledgeAsync("{}");

        Assert.Empty(result.Result);
        Assert.Null(result.Usage);
    }

    private static LlmClient CreateClient(
        Guid chatRouteId,
        Guid embeddingRouteId,
        Func<string, HttpRequestMessage, HttpResponseMessage> responseFactory)
    {
        var inference = new Mock<IInferenceConfigurationAppService>();
        inference
            .Setup(item => item.GetRuntimeOptionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new InferenceRuntimeOptions(
                new InferenceRuntimeRoute(chatRouteId, "https://provider.test/v1", "test-key", "chat-model-snapshot"),
                new InferenceRuntimeRoute(embeddingRouteId, "https://provider.test/v1", "test-key", "embedding-model-snapshot")));

        var settings = new Mock<ISettingsAppService>();
        settings
            .Setup(item => item.GetLanguageAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync("en-US");

        var translations = new Mock<ITranslationService>();
        translations
            .Setup(item => item.Get(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object?[]>()))
            .Returns("Extract reusable knowledge.");

        var handler = new StubHttpMessageHandler((request, _) =>
            responseFactory(
                request.RequestUri?.AbsolutePath.EndsWith("/embeddings", StringComparison.Ordinal) == true
                    ? "embedding"
                    : "chat",
                request));

        return new LlmClient(
            settings.Object,
            translations.Object,
            inference.Object,
            new HttpClient(handler));
    }

    private static HttpResponseMessage ChatResponse(bool includeUsage)
        => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                id = "chat-response",
                model = "chat-model-snapshot",
                choices = new[]
                {
                    new
                    {
                        index = 0,
                        message = new { role = "assistant", content = "{\"knowledgeItems\":[]}" },
                        finish_reason = "stop",
                    },
                },
                usage = includeUsage
                    ? new
                    {
                        prompt_tokens = 100,
                        completion_tokens = 40,
                        total_tokens = 140,
                        prompt_tokens_details = new { cached_tokens = 25 },
                    }
                    : null,
            }),
        };

    private static HttpResponseMessage EmbeddingResponse(bool includeUsage)
        => new(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                object_name = "list",
                data = new[]
                {
                    new { object_name = "embedding", index = 0, embedding = new float[LearningPipelineDefaults.EmbeddingDimensions] },
                },
                model = "embedding-model-snapshot",
                usage = includeUsage
                    ? new { prompt_tokens = 12, total_tokens = 12 }
                    : null,
            }),
        };

    private sealed class StubHttpMessageHandler(
        Func<HttpRequestMessage, CancellationToken, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
            => Task.FromResult(responseFactory(request, cancellationToken));
    }
}
