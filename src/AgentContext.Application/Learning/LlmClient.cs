using System.ClientModel;
using System.ClientModel.Primitives;
using System.Text.Json;
using System.Text.Json.Serialization;
using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI;
using OpenAI.Chat;

namespace AgentContext.Application.Learning;

/// <inheritdoc cref="ILlmClient"/>
/// <summary>
/// Production <see cref="ILlmClient"/> built on the Microsoft Agent Framework
/// (microsoft/agent-framework): extraction runs as an <see cref="AIAgent"/>
/// (Chat Completions provider via <c>Microsoft.Agents.AI.OpenAI</c>) with a
/// structured-output <c>RunAsync&lt;T&gt;</c>; embedding runs through MAF's AI
/// abstraction layer (<c>IEmbeddingGenerator</c>). Both hit the same configured
/// OpenAI-compatible endpoint (ADR 0003 — one base URL + key serves both; the
/// endpoint option is the custom-base-URL seam for Ollama / LM Studio / gateways).
/// </summary>
public sealed class LlmClient : ILlmClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly AIAgent _agent;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;

    public LlmClient(IOptions<LlmOptions> options, HttpClient? httpClient = null)
    {
        // Resolving .Value runs LlmOptionsValidator (invalid config throws
        // OptionsValidationException here — the worker tick records it).
        var settings = options.Value;

        var openAiOptions = new OpenAIClientOptions { Endpoint = new Uri(settings.BaseUrl) };
        if (httpClient is not null)
        {
            // Custom transport lets tests stub requests; production uses the SDK default.
            openAiOptions.Transport = new HttpClientPipelineTransport(httpClient);
        }

        var credential = new ApiKeyCredential(settings.ApiKey);
        var openAiClient = new OpenAIClient(credential, openAiOptions);

        // MAF: the Learning Engine extraction step as an agent over the configured endpoint.
        _agent = openAiClient.GetChatClient(settings.Model).AsAIAgent(
            instructions: ExtractionSystemPrompt,
            name: "learning-engine");

        // MAF AI abstraction: embedding generator over the same endpoint.
        _embeddingGenerator =
            new OpenAI.Embeddings.EmbeddingClient(settings.EffectiveEmbeddingModel, credential, openAiOptions)
                .AsIEmbeddingGenerator();
    }

    public async Task<IReadOnlyList<KnowledgeExtraction>> ExtractKnowledgeAsync(
        string sessionSummaryJson, CancellationToken cancellationToken = default)
    {
        // Structured output: RunAsync<T> deserializes the agent's JSON reply.
        // (session: null → a fresh one-off run; options: null → agent defaults.)
        var response = await _agent.RunAsync<ExtractionEnvelope>(sessionSummaryJson, null, JsonOptions, null, cancellationToken);
        return response.Result?.KnowledgeItems ?? [];
    }

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        var generated = await _embeddingGenerator.GenerateAsync([text], cancellationToken: cancellationToken);
        var vector = generated[0].Vector;

        if (vector.Length != LearningPipelineDefaults.EmbeddingDimensions)
        {
            throw new InvalidOperationException(
                $"Embedding endpoint returned {vector.Length} dimensions; the schema is fixed at " +
                $"{LearningPipelineDefaults.EmbeddingDimensions} (vector(1536)). Point Llm:EmbeddingModel at a " +
                "1536-dim model (e.g. text-embedding-3-small) or migrate the column.");
        }

        return vector.ToArray();
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
