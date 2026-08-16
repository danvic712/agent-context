using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
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
/// OpenAI-compatible endpoint (ADR 0003). The endpoint is resolved from the
/// database on every call (<see cref="ISettingsAppService"/>) so a settings
/// change takes effect without a restart.
/// </summary>
public sealed class LlmClient(ISettingsAppService settings, HttpClient? httpClient = null) : ILlmClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public async Task<IReadOnlyList<KnowledgeExtraction>> ExtractKnowledgeAsync(
        string sessionSummaryJson, CancellationToken cancellationToken = default)
    {
        var options = ResolveOptions(await settings.GetLlmOptionsAsync(cancellationToken));

        // MAF structured output: RunAsync<T> deserializes the agent's JSON reply.
        // (session: null → a fresh one-off run; options: null → agent defaults.)
        var response = await CreateAgent(options).RunAsync<ExtractionEnvelope>(
            sessionSummaryJson, null, JsonOptions, null, cancellationToken);

        return response.Result?.KnowledgeItems ?? [];
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var options = ResolveOptions(await settings.GetLlmOptionsAsync(cancellationToken));

        var generated = await CreateGenerator(options).GenerateAsync([text], cancellationToken: cancellationToken);
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

    private static LlmOptions ResolveOptions(LlmOptions? options) =>
        options ?? throw new InvalidOperationException(
            "The LLM endpoint is not configured. Save the LLM settings (BaseUrl/ApiKey/Model) first.");

    private AIAgent CreateAgent(LlmOptions options)
    {
        var openAiOptions = CreateClientOptions(options.BaseUrl);
        var credential = new ApiKeyCredential(options.ApiKey);
        return new OpenAIClient(credential, openAiOptions)
            .GetChatClient(options.Model)
            .AsAIAgent(instructions: ExtractionSystemPrompt, name: "learning-engine");
    }

    private IEmbeddingGenerator<string, Embedding<float>> CreateGenerator(LlmOptions options)
    {
        var openAiOptions = CreateClientOptions(options.BaseUrl);
        var credential = new ApiKeyCredential(options.ApiKey);
        return new OpenAI.Embeddings.EmbeddingClient(options.EffectiveEmbeddingModel, credential, openAiOptions)
            .AsIEmbeddingGenerator();
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

    private const string ExtractionSystemPrompt =
        """
        You are the Learning Engine of a knowledge platform. Extract reusable Knowledge items
        (Problem / Solution / Pattern) from the agent session summary provided by the user.

        Rules:
        - "Problem": a recurring problem described in the session.
        - "Solution": a concrete way to solve a problem, with enough context to be reusable.
        - "Pattern": a generalisable approach or convention that applies beyond this session.
        - title: short and self-contained (max ~80 chars). content: the reusable knowledge itself, 1-3 sentences.
        - selfAssessment: 0..1 — how confident you are that this item is correct and useful.
        - Output STRICT JSON only, exactly this shape:
          {"knowledgeItems":[{"type":"Problem"|"Solution"|"Pattern","title":"...","content":"...","selfAssessment":0.8}]}
        - Use an empty array when the summary contains nothing reusable. Do not invent knowledge absent from the summary.
        """;
}
