namespace AgentContext.Application.Dtos;

/// <summary>Result of verifying the unsaved Chat and Embedding draft.</summary>
public sealed record InferenceValidationResult(
    bool Valid,
    IReadOnlyList<InferenceValidationCheck> Checks);
