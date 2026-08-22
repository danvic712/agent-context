using System.Net;
using System.Net.Http.Json;
using AgentContext.Application.Contracts;
using AgentContext.Application.Inference;
using AgentContext.Application.Localization;
using AgentContext.Application.Tests.TestSupport;
using AgentContext.Domain;
using AgentContext.Infrastructure;
using Moq;
using Moq.Protected;

namespace AgentContext.Application.Tests.Inference;

public sealed class InferenceConfigurationAppServiceTests
{
    [Fact]
    public async Task Get_returns_unconfigured_projection_with_seeded_providers()
    {
        var context = MockInferenceDbContext.Create(
            providers:
            [
                InferenceTestData.OpenAiProvider(string.Empty),
                InferenceTestData.DeepSeekProvider(string.Empty),
            ]);
        var service = CreateService(context.Object);

        var result = await service.GetAsync();

        Assert.False(result.Configured);
        Assert.Null(result.Id);
        Assert.Null(result.Name);
        Assert.Collection(
            result.Providers,
            openAi =>
            {
                Assert.Equal("OpenAI", openAi.Name);
                Assert.False(openAi.ApiKeyConfigured);
                Assert.Null(openAi.MaskedApiKey);
            },
            deepSeek =>
            {
                Assert.Equal("DeepSeek", deepSeek.Name);
                Assert.False(deepSeek.ApiKeyConfigured);
                Assert.Null(deepSeek.MaskedApiKey);
            });
    }

    [Fact]
    public async Task Get_returns_configured_projection_with_routes_and_masked_provider_secrets()
    {
        var configuration = InferenceTestData.ConfiguredConfiguration();
        configuration.Routes.Add(InferenceTestData.ChatRoute());
        configuration.Routes.Add(InferenceTestData.EmbeddingRoute());
        var context = MockInferenceDbContext.Create(
            configurations: [configuration],
            providers:
            [
                InferenceTestData.OpenAiProvider(),
                InferenceTestData.DeepSeekProvider(),
            ]);
        var service = CreateService(context.Object);

        var result = await service.GetAsync();

        Assert.True(result.Configured);
        Assert.Equal(InferenceTestData.ConfigurationId, result.Id);
        Assert.Equal("Platform default", result.Name);
        Assert.Equal(2, result.Routes.Count);
        Assert.Equal(
            (InferenceCapability.Chat, InferenceTestData.OpenAiProviderId, "gpt-4o-mini"),
            (result.Routes[0].Capability, result.Routes[0].ProviderId, result.Routes[0].Model));
        Assert.Equal(
            (InferenceCapability.Embedding, InferenceTestData.DeepSeekProviderId, "text-embedding-3-small"),
            (result.Routes[1].Capability, result.Routes[1].ProviderId, result.Routes[1].Model));
        Assert.All(result.Providers, provider =>
        {
            Assert.True(provider.ApiKeyConfigured);
            Assert.Equal("••••••", provider.MaskedApiKey);
        });
    }

    [Fact]
    public async Task Verify_tests_chat_and_embedding_routes_against_different_providers()
    {
        var requests = new List<(string Uri, string Authorization, string Body)>();
        using var client = CreateValidationClient(requests, embeddingDimensions: 1536);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(item => item.CreateClient("inference-validation")).Returns(client);
        var secrets = new Mock<IInferenceSecretProtector>(MockBehavior.Strict);
        var context = MockInferenceDbContext.Create();
        var service = new InferenceConfigurationAppService(context.Object, secrets.Object, factory.Object);

        var result = await service.VerifyAsync(InferenceTestData.ValidInput());

        Assert.True(result.Valid);
        Assert.Collection(
            result.Checks,
            chat => Assert.True(chat.Valid),
            embedding => Assert.True(embedding.Valid));
        Assert.Equal(2, requests.Count);
        Assert.Equal("https://api.openai.com/v1/chat/completions", requests[0].Uri);
        Assert.Equal("https://api.deepseek.com/v1/embeddings", requests[1].Uri);
        Assert.Equal("sk-openai-test", requests[0].Authorization);
        Assert.Equal("sk-deepseek-test", requests[1].Authorization);
        Assert.Contains("gpt-4o-mini", requests[0].Body);
        Assert.Contains("text-embedding-3-small", requests[1].Body);
    }

    [Fact]
    public async Task Verify_is_invalid_when_either_required_route_fails()
    {
        var requests = new List<(string Uri, string Authorization, string Body)>();
        using var client = CreateValidationClient(requests, embeddingDimensions: 1);
        var factory = new Mock<IHttpClientFactory>();
        factory.Setup(item => item.CreateClient("inference-validation")).Returns(client);
        var context = MockInferenceDbContext.Create();
        var service = new InferenceConfigurationAppService(
            context.Object,
            Mock.Of<IInferenceSecretProtector>(),
            factory.Object);

        var result = await service.VerifyAsync(InferenceTestData.ValidInput());

        Assert.False(result.Valid);
        Assert.True(result.Checks.Single(check => check.Capability == InferenceCapability.Chat).Valid);
        var embedding = result.Checks.Single(check => check.Capability == InferenceCapability.Embedding);
        Assert.False(embedding.Valid);
        Assert.Contains("1536", embedding.Message);
    }

    [Fact]
    public async Task Verify_rejects_a_provider_without_an_api_key_before_network_calls()
    {
        var factory = new Mock<IHttpClientFactory>(MockBehavior.Strict);
        var context = MockInferenceDbContext.Create();
        var service = new InferenceConfigurationAppService(
            context.Object,
            Mock.Of<IInferenceSecretProtector>(),
            factory.Object);

        var exception = await Assert.ThrowsAsync<LocalizedException>(() =>
            service.VerifyAsync(InferenceTestData.ValidInput(deepSeekApiKey: null)));

        Assert.Equal(ErrorCodes.Inference.ApiKeyRequired, exception.ErrorCode);
        factory.Verify(item => item.CreateClient(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task GetRuntimeOptions_resolves_chat_and_embedding_from_different_providers()
    {
        var configuration = InferenceTestData.ConfiguredConfiguration();
        configuration.Routes.Add(InferenceTestData.ChatRoute());
        configuration.Routes.Add(InferenceTestData.EmbeddingRoute());
        var context = MockInferenceDbContext.Create(
            configurations: [configuration],
            providers:
            [
                InferenceTestData.OpenAiProvider(),
                InferenceTestData.DeepSeekProvider(),
            ]);
        var secrets = new Mock<IInferenceSecretProtector>();
        secrets.Setup(item => item.Unprotect("protected-openai")).Returns("sk-openai-runtime");
        secrets.Setup(item => item.Unprotect("protected-deepseek")).Returns("sk-deepseek-runtime");
        var service = CreateService(context.Object, secrets);

        var options = await service.GetRuntimeOptionsAsync();

        Assert.NotNull(options);
        Assert.Equal("https://api.openai.com/v1", options!.Chat.BaseUrl);
        Assert.Equal(InferenceTestData.ChatRouteId, options.Chat.Id);
        Assert.Equal("sk-openai-runtime", options.Chat.ApiKey);
        Assert.Equal("gpt-4o-mini", options.Chat.Model);
        Assert.Equal("https://api.deepseek.com/v1", options.Embedding.BaseUrl);
        Assert.Equal(InferenceTestData.EmbeddingRouteId, options.Embedding.Id);
        Assert.Equal("sk-deepseek-runtime", options.Embedding.ApiKey);
        Assert.Equal("text-embedding-3-small", options.Embedding.Model);
    }

    private static InferenceConfigurationAppService CreateService(
        AgentContextDbContext db,
        Mock<IInferenceSecretProtector>? secrets = null)
    {
        var factory = new Mock<IHttpClientFactory>();
        return new InferenceConfigurationAppService(
            db,
            secrets?.Object ?? Mock.Of<IInferenceSecretProtector>(),
            factory.Object);
    }

    private static HttpClient CreateValidationClient(
        ICollection<(string Uri, string Authorization, string Body)> requests,
        int embeddingDimensions)
    {
        var handler = new Mock<HttpMessageHandler>();
        handler
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .Returns(async (HttpRequestMessage request, CancellationToken _) =>
            {
                var body = await request.Content!.ReadAsStringAsync();
                requests.Add((
                    request.RequestUri!.ToString(),
                    request.Headers.Authorization?.Parameter ?? string.Empty,
                    body));

                if (request.RequestUri.AbsolutePath.EndsWith("/chat/completions", StringComparison.Ordinal))
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = JsonContent.Create(new { choices = new[] { new { text = "OK" } } }),
                    };
                }

                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = JsonContent.Create(new
                    {
                        data = new[]
                        {
                            new { embedding = new float[embeddingDimensions] },
                        },
                    }),
                };
            });

        return new HttpClient(handler.Object);
    }
}
