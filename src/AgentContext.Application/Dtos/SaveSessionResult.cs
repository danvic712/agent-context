namespace AgentContext.Application.Dtos;

/// <summary>Outcome of a successful save_session call.</summary>
public sealed record SaveSessionResult(Guid SessionId, string? DomainName, bool Remembered);
