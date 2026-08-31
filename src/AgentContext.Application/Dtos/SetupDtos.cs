namespace AgentContext.Application.Dtos;

/// <summary>Current setup state of the platform (first-run wizard gate).</summary>
public sealed record SetupStatus(bool Configured, string? WorkspaceName = null);

/// <summary>
/// Input for the first-run wizard: account/preferences plus an optional
/// inference configuration. An omitted or untouched inference draft defers
/// model setup until Settings.
/// </summary>
public sealed record SetupRequest(
    string DisplayName,
    string Email,
    string Password,
    string Language = "en-US",
    InferenceConfigurationInput? InferenceConfiguration = null);

/// <summary>Result of a successful first-run configuration.</summary>
public sealed record SetupResult(Guid UserId, Guid WorkspaceId, string WorkspaceName);
