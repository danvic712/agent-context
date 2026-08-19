namespace AgentContext.Domain.Entities;

/// <summary>
/// The platform-level inference configuration. Routes and providers are kept
/// in separate tables so chat and embedding can use different services/models.
/// </summary>
public sealed class InferenceConfiguration
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Name { get; set; } = "Platform default";

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<InferenceRoute> Routes { get; set; } = new List<InferenceRoute>();
}
