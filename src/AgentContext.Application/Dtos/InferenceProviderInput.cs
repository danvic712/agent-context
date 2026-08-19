namespace AgentContext.Application.Dtos;

/// <summary>Write model for one provider. ApiKey is write-only and may be blank to retain it.</summary>
public sealed record InferenceProviderInput(
    Guid Id,
    string Name,
    string ProviderType,
    string BaseUrl,
    string? ApiKey);
