using AgentContext.Application.Dtos;
namespace AgentContext.Application.Contracts;

/// <summary>
/// Session recording service (T2): persists a reported Session with Usage
/// attached, resolves/creates the domain tag, honours the remember flag, and
/// exposes the recorded data for the session overview. Primary test seam.
/// </summary>
public interface ISaveSessionAppService
{
    Task<SaveSessionResult> SaveAsync(SaveSessionRequest request, CancellationToken cancellationToken = default);

    Task<SessionDetail> GetAsync(Guid sessionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SessionListItem>> ListAsync(CancellationToken cancellationToken = default);
}
