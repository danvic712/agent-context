namespace AgentContext.Application.Dtos;

/// <summary>
/// The LLM endpoint configuration as exposed over REST (T10): whether it is
/// configured, the base URL + model(s), and the API key always masked — never
/// returned in full.
/// </summary>
public sealed record LlmOptionsDto(
    bool Configured,
    string? BaseUrl,
    string? MaskedApiKey,
    string? Model,
    string? EmbeddingModel);
