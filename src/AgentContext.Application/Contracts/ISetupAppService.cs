namespace AgentContext.Application.Contracts;

/// <summary>Current setup state of the platform (first-run wizard gate).</summary>
public sealed record SetupStatus(bool Configured);

/// <summary>Input for the first-run wizard: creates the admin User + Personal Workspace.</summary>
public sealed record SetupRequest(string DisplayName, string Email, string Password);

/// <summary>Result of a successful first-run configuration.</summary>
public sealed record SetupResult(Guid UserId, Guid WorkspaceId, string WorkspaceName);

/// <summary>
/// Thrown when ConfigureAsync is called after the platform has already been
/// configured — rerunning the wizard is blocked (T1 acceptance criterion).
/// </summary>
public sealed class SetupAlreadyConfiguredException : InvalidOperationException
{
    public SetupAlreadyConfiguredException()
        : base("The platform has already been configured. Rerunning the first-run wizard is blocked.") { }
}

/// <summary>
/// First-run wizard service. Runs against a real Postgres via the application
/// service seam (the primary test seam per spec §Testing Decisions).
/// </summary>
public interface ISetupAppService
{
    Task<SetupStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<SetupResult> ConfigureAsync(SetupRequest request, CancellationToken cancellationToken = default);
}
