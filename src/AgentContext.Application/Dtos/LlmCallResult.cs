namespace AgentContext.Application.Dtos;

/// <summary>
/// Functional result and provider metadata from one Learning Engine inference call.
/// Usage is null when the provider did not return usable token counts.
/// </summary>
public sealed record LlmCallResult<T>(
    T Result,
    LlmUsage? Usage,
    Guid InferenceRouteId,
    string Model);
