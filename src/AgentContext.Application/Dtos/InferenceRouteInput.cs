using AgentContext.Domain;

namespace AgentContext.Application.Dtos;

/// <summary>Write model for one capability route.</summary>
public sealed record InferenceRouteInput(
    Guid Id,
    InferenceCapability Capability,
    Guid ProviderId,
    string Model);
