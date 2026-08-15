namespace AgentContext.Application.Dtos;

/// <summary>Current setup state of the platform (first-run wizard gate).</summary>
public sealed record SetupStatus(bool Configured);

/// <summary>Input for the first-run wizard: creates the admin User + Personal Workspace.</summary>
public sealed record SetupRequest(string DisplayName, string Email, string Password);

/// <summary>Result of a successful first-run configuration.</summary>
public sealed record SetupResult(Guid UserId, Guid WorkspaceId, string WorkspaceName);
