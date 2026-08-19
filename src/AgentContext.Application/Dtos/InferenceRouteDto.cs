using AgentContext.Domain;

namespace AgentContext.Application.Dtos;

/// <summary>A capability-to-provider model binding.</summary>
public sealed record InferenceRouteDto(
    Guid Id,
    InferenceCapability Capability,
    Guid ProviderId,
    string Model);
