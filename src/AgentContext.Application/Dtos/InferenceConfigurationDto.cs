namespace AgentContext.Application.Dtos;

/// <summary>The platform-level inference configuration and its three-table projection.</summary>
public sealed record InferenceConfigurationDto(
    bool Configured,
    Guid? Id,
    string? Name,
    IReadOnlyList<InferenceProviderDto> Providers,
    IReadOnlyList<InferenceRouteDto> Routes,
    DateTimeOffset? UpdatedAtUtc);
