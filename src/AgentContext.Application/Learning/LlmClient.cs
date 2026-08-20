using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Application.Localization;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace AgentContext.Application.Learning;

/// <inheritdoc cref="ILlmClient"/>
/// <summary>
/// Production <see cref="ILlmClient"/> built on the Microsoft Agent Framework
/// (microsoft/agent-framework): extraction runs as an <see cref="AIAgent"/>
/// (Chat Completions provider via <c>Microsoft.Agents.AI.OpenAI</c>) with a
/// structured-output <c>RunAsync&lt;T&gt;</c>; embedding runs through MAF's AI
/// abstraction layer (<c>IEmbeddingGenerator</c>). Both hit the configured
/// OpenAI-compatible routes (ADR 0003), resolved from the dedicated inference
/// configuration on every call so a provider or route change takes effect
/// without a restart. The extraction prompt (T11) is
/// resolved from the shared JSON store in the configured language — the model
/// writes Problem/Solution/Pattern in that language while keeping code
/// identifiers, technical terms and key original snippets verbatim.
/// </summary>
public sealed class LlmClient(
    ISettingsAppService settings,
    ITranslationService translations,
    IInferenceConfigurationAppService inference,
    HttpClient? httpClient = null) : ILlmClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<IReadOnlyList<KnowledgeExtraction>> ExtractKnowledgeAsync(
        string sessionSummaryJson, CancellationToken cancellationToken = default)
    {
        var runtime = await GetRuntimeOptionsAsync(cancellationToken);
        var locale = await settings.GetLanguageAsync(cancellationToken);

        // MAF structured output: RunAsync<T> deserializes the agent's JSON reply.
        // (session: null → a fresh one-off run; options: null → agent defaults.)
        var response = await CreateAgent(runtime.Chat, locale).RunAsync<ExtractionEnvelope>(
            sessionSummaryJson, null, JsonOptions, null, cancellationToken);

        return response.Result?.KnowledgeItems ?? [];
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var runtime = await GetRuntimeOptionsAsync(cancellationToken);
        var generated = await CreateGenerator(runtime.Embedding).GenerateAsync([text], cancellationToken: cancellationToken);
        var vector = generated[0].Vector;

        if (vector.Length != LearningPipelineDefaults.EmbeddingDimensions)
        {
            throw new InvalidOperationException(
                $"Embedding endpoint returned {vector.Length} dimensions; the schema is fixed at " +
                $"{LearningPipelineDefaults.EmbeddingDimensions} (vector(1536)). Configure an embedding model " +
                "with 1536 dimensions (e.g. text-embedding-3-small) or migrate the column.");
        }

        return vector.ToArray();
    }

    private async Task<InferenceRuntimeOptions> GetRuntimeOptionsAsync(CancellationToken cancellationToken)
        => await inference.GetRuntimeOptionsAsync(cancellationToken)
            ?? throw new LocalizedException(
                System.Net.HttpStatusCode.InternalServerError,
                ErrorCodes.Inference.NotConfigured);

    private AIAgent CreateAgent(InferenceRuntimeRoute route, string locale)
    {
        var openAiOptions = CreateClientOptions(route.BaseUrl);
        var credential = new ApiKeyCredential(route.ApiKey);
        return new OpenAIClient(credential, openAiOptions)
            .GetChatClient(route.Model)
            .AsAIAgent(instructions: BuildExtractionPrompt(locale), name: "learning-engine");
    }

    /// <summary>
    /// The extraction prompt (T11, mixed mode): the shared JSON store's
    /// <c>prompts.extraction</c> template for the configured locale — en-US and
    /// zh-CN instruct output in that language while preserving identifiers and
    /// key original snippets.
    /// </summary>
    private string BuildExtractionPrompt(string locale) => translations.Get("prompts.extraction", locale);

    private IEmbeddingGenerator<string, Embedding<float>> CreateGenerator(InferenceRuntimeRoute route)
    {
        var openAiOptions = CreateClientOptions(route.BaseUrl);
        var credential = new ApiKeyCredential(route.ApiKey);
        // Pass the schema's fixed dimension explicitly: the OpenAI-compatible
        // endpoint must return vector(1536). Some deployments (e.g. Azure
        // text-embedding-3-large) default to a different size and only honor
        // the requested dimension when told (T9 integration fix).
        return new OpenAI.Embeddings.EmbeddingClient(route.Model, credential, openAiOptions)
            .AsIEmbeddingGenerator(LearningPipelineDefaults.EmbeddingDimensions);
    }

    private OpenAIClientOptions CreateClientOptions(string baseUrl)
    {
        var openAiOptions = new OpenAIClientOptions { Endpoint = new Uri(baseUrl) };
        if (httpClient is not null)
        {
            // Custom transport lets tests stub requests; production uses the SDK default.
            openAiOptions.Transport = new HttpClientPipelineTransport(httpClient);
        }

        return openAiOptions;
    }

    private sealed record ExtractionEnvelope(IReadOnlyList<KnowledgeExtraction>? KnowledgeItems);
}
