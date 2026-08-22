using AgentContext.Application.Dtos;

namespace AgentContext.Application.Contracts;

/// <summary>
/// The Learning Engine's LLM surface (ADR 0003): one OpenAI-compatible endpoint
/// serves extraction and embedding in v1. Production implementation talks to the
/// configured endpoint through the Microsoft Agent Framework AI layer
/// (<c>IChatClient</c> / <c>IEmbeddingGenerator</c>); tests inject a fake, so the
/// LLM is the mocked collaborator at the seam — never the database.
/// </summary>
public interface ILlmClient
{
    /// <summary>
    /// Extracts Knowledge items (Problem / Solution / Pattern) from a session
    /// summary document. Returns zero or more items plus provider usage and
    /// route/model metadata; empty means the summary contained nothing reusable.
    /// </summary>
    Task<LlmCallResult<IReadOnlyList<KnowledgeExtraction>>> ExtractKnowledgeAsync(
        string sessionSummaryJson, CancellationToken cancellationToken = default);

    /// <summary>
    /// Embeds a text into a fixed-dimension vector for pgvector storage and
    /// returns provider usage plus route/model metadata.
    /// Throws when the endpoint returns a dimension different from the schema.
    /// </summary>
    Task<LlmCallResult<float[]>> EmbedAsync(string text, CancellationToken cancellationToken = default);
}
