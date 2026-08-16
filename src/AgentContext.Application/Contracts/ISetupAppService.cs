using AgentContext.Application.Dtos;

namespace AgentContext.Application.Contracts;

/// <summary>
/// First-run wizard service. Runs against a real Postgres via the application
/// service seam (the primary test seam per spec §Testing Decisions).
/// Rerunning after configuration is blocked with a 409 coded error
/// (<c>setup.alreadyConfigured</c>).
/// </summary>
public interface ISetupAppService
{
    Task<SetupStatus> GetStatusAsync(CancellationToken cancellationToken = default);

    Task<SetupResult> ConfigureAsync(SetupRequest request, CancellationToken cancellationToken = default);
}
