using AgentContext.Domain;

namespace AgentContext.Application.Dtos;

/// <summary>One source-aware Usage row exposed on a session detail.</summary>
public sealed record SessionUsageDto(
    string Model,
    int InputTokens,
    int CachedInputTokens,
    int OutputTokens,
    string Source,
    Guid? InferenceRouteId,
    InferenceCapability? Capability);
