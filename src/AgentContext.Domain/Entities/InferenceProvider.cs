namespace AgentContext.Domain.Entities;

/// <summary>
/// A reusable OpenAI-compatible provider connection. The API key is stored as
/// protected secret material in ApiKeySecretRef and is never returned by APIs.
/// </summary>
public sealed class InferenceProvider
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Name { get; set; } = string.Empty;

    public string ProviderType { get; set; } = "openai-compatible";

    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKeySecretRef { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<InferenceRoute> Routes { get; set; } = new List<InferenceRoute>();
}
