namespace AgentContext.Application.Dtos;

/// <summary>Current setup state of the platform (first-run wizard gate).</summary>
public sealed record SetupStatus(bool Configured);

/// <summary>Input for the first-run wizard: account/preferences plus inference configuration.</summary>
public sealed record SetupRequest(
    string DisplayName,
    string Email,
    string Password,
    string Language = "en-US",
    InferenceConfigurationInput? InferenceConfiguration = null);

/// <summary>Result of a successful first-run configuration.</summary>
public sealed record SetupResult(Guid UserId, Guid WorkspaceId, string WorkspaceName);
