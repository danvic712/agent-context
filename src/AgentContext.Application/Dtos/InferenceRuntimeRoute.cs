namespace AgentContext.Application.Dtos;

/// <summary>Resolved provider credentials used internally by the Learning Engine.</summary>
public sealed record InferenceRuntimeRoute(
    string BaseUrl,
    string ApiKey,
    string Model);
