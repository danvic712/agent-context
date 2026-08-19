using AgentContext.Domain;

namespace AgentContext.Domain.Entities;

/// <summary>Maps one model capability to a provider connection.</summary>
public sealed class InferenceRoute
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid InferenceConfigurationId { get; set; }

    public InferenceCapability Capability { get; set; }

    public Guid ProviderId { get; set; }

    public string Model { get; set; } = string.Empty;

    public InferenceConfiguration InferenceConfiguration { get; set; } = null!;

    public InferenceProvider Provider { get; set; } = null!;
}
