namespace AgentContext.Application.Dtos;

/// <summary>Separate runtime routes allow Chat and Embedding to use different providers.</summary>
public sealed record InferenceRuntimeOptions(
    InferenceRuntimeRoute Chat,
    InferenceRuntimeRoute Embedding);
