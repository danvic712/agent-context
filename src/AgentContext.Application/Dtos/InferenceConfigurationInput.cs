namespace AgentContext.Application.Dtos;

/// <summary>Write model shared by Settings and the first-run Setup flow.</summary>
public sealed record InferenceConfigurationInput(
    string Name,
    IReadOnlyList<InferenceProviderInput> Providers,
    IReadOnlyList<InferenceRouteInput> Routes);
