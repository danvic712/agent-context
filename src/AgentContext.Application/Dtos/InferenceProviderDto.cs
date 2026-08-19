namespace AgentContext.Application.Dtos;

/// <summary>The provider connection returned by the inference configuration API.</summary>
public sealed record InferenceProviderDto(
    Guid Id,
    string Name,
    string ProviderType,
    string BaseUrl,
    bool ApiKeyConfigured,
    string? MaskedApiKey,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);
