using AgentContext.Domain;

namespace AgentContext.Application.Dtos;

/// <summary>One non-persisting connection verification result.</summary>
public sealed record InferenceValidationCheck(
    InferenceCapability Capability,
    bool Valid,
    string? Message);
