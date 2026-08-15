using System.Net;
using System.Text;
using System.Text.Json;
using AgentContext.Application.Contracts;
using AgentContext.Application.Learning;
using AgentContext.Domain;
using Microsoft.Extensions.Options;

namespace AgentContext.Tests.SeamTests;

/// <summary>
/// Request-shape tests for the real <see cref="LlmClient"/> (Microsoft Agent
/// Framework AI layer: IChatClient / IEmbeddingGenerator over the OpenAI
/// SDK), driven through a stub HttpMessageHandler so no network is involved.
/// The pipeline seam tests use a fake ILlmClient; these prove the real client
/// talks to the configured OpenAI-compatible endpoint as expected (ADR 0003).
/// </summary>
public sealed class LlmClientTests
{
    private const int Dimensions = LearningPipelineDefaults.EmbeddingDimensions;

    [Fact]
    public async Task ExtractKnowledgeAsync_posts_to_the_configured_chat_completions_endpoint()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(OpenAiChatCompletion(
            """{"knowledgeItems":[{"type":"Solution","title":"Fix","content":"Do X","selfAssessment":0.8}]}""")));
        var client = CreateClient(handler: handler);

        var items = await client.ExtractKnowledgeAsync("""{"task":"t","conclusion":"c"}""");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(new Uri("http://localhost:11434/v1/chat/completions"), request.RequestUri);
        Assert.Equal("Bearer test-key", request.Authorization);

        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal("llama3.2", body.RootElement.GetProperty("model").GetString());
        // MAF's RunAsync<T> sends a structured json_schema derived from the result type.
        Assert.Equal("json_schema", body.RootElement.GetProperty("response_format").GetProperty("type").GetString());
        Assert.Equal(2, body.RootElement.GetProperty("messages").GetArrayLength());

        var item = Assert.Single(items);
        Assert.Equal(KnowledgeType.Solution, item.Type);
        Assert.Equal("Fix", item.Title);
        Assert.Equal("Do X", item.Content);
        Assert.Equal(0.8, item.SelfAssessment);
    }

    [Fact]
    public async Task ExtractKnowledgeAsync_returns_empty_for_no_items()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(OpenAiChatCompletion("""{"knowledgeItems":[]}""")));
        var client = CreateClient(handler: handler);

        var items = await client.ExtractKnowledgeAsync("{}");

        Assert.Empty(items);
    }

    [Fact]
    public async Task EmbedAsync_posts_to_the_configured_embeddings_endpoint_with_the_embedding_model()
    {
        var vector = Enumerable.Range(0, Dimensions).Select(i => (float)i / Dimensions).ToArray();
        var handler = new StubHttpMessageHandler(_ => JsonResponse(OpenAiEmbeddings(vector)));
        var client = CreateClient(o => o.EmbeddingModel = "nomic-embed-text", handler);

        var embedding = await client.EmbedAsync("some text");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(new Uri("http://localhost:11434/v1/embeddings"), request.RequestUri);

        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal("nomic-embed-text", body.RootElement.GetProperty("model").GetString());
        // The SDK serializes input as an array (multi-input is supported).
        Assert.Equal("some text", body.RootElement.GetProperty("input")[0].GetString());

        Assert.Equal(Dimensions, embedding.Length);
        Assert.Equal(vector[0], embedding[0]);
        Assert.Equal(vector[^1], embedding[^1]);
    }

    [Fact]
    public async Task EmbedAsync_falls_back_to_the_extraction_model_when_embedding_model_omitted()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(OpenAiEmbeddings(new float[Dimensions])));
        var client = CreateClient(handler: handler);

        await client.EmbedAsync("text");

        using var body = JsonDocument.Parse(handler.Requests[0].Body!);
        Assert.Equal("llama3.2", body.RootElement.GetProperty("model").GetString());
    }

    [Fact]
    public async Task EmbedAsync_rejects_a_dimension_mismatch()
    {
        var handler = new StubHttpMessageHandler(_ => JsonResponse(OpenAiEmbeddings([1f, 2f, 3f])));
        var client = CreateClient(handler: handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => client.EmbedAsync("text"));

        Assert.Contains("1536", ex.Message);
    }

    private static LlmClient CreateClient(Action<LlmOptions>? configure = null, StubHttpMessageHandler? handler = null)
    {
        var options = new LlmOptions
        {
            BaseUrl = "http://localhost:11434/v1",
            ApiKey = "test-key",
            Model = "llama3.2",
        };
        configure?.Invoke(options);
        return new LlmClient(Options.Create(options), handler?.ToHttpClient());
    }

    private static HttpResponseMessage JsonResponse(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };

    private static string OpenAiChatCompletion(string contentJson)
    {
        var escaped = contentJson.Replace("\"", "\\\"");
        return $$$"""
            {"id":"chatcmpl-test","object":"chat.completion","created":1,"model":"llama3.2",
             "choices":[{"index":0,"finish_reason":"stop","message":{"role":"assistant","content":"{{{escaped}}}"}}],
             "usage":{"prompt_tokens":1,"completion_tokens":1,"total_tokens":2}}
            """;
    }

    private static string OpenAiEmbeddings(float[] vector) =>
        $$$"""
          {"object":"list","data":[{"object":"embedding","index":0,"embedding":[{{{string.Join(",", vector)}}}]}],
           "model":"text-embedding-3-small","usage":{"prompt_tokens":1,"total_tokens":1}}
          """;

    private sealed class StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        public HttpClient ToHttpClient() => new(this);

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            // The OpenAI SDK disposes the request content after the call, so capture
            // the body here — assertions read the capture, not a disposed stream.
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(
                request.RequestUri, request.Headers.Authorization?.ToString(), body));
            return respond(request);
        }
    }

    private sealed record CapturedRequest(Uri? RequestUri, string? Authorization, string? Body);
}
