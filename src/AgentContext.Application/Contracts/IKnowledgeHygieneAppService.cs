using AgentContext.Application.Dtos;

namespace AgentContext.Application.Contracts;

/// <summary>
/// Knowledge hygiene (T8 / spec US20): a PeriodicTimer-driven job that applies
/// temporal decay to long-unused Knowledge and moves decayed / low-Confidence
/// items to Review, then Archives Review items left untouched. Runs idempotently
/// — safe to re-run at any frequency (AC2).
/// </summary>
public interface IKnowledgeHygieneAppService
{
    /// <summary>
    /// Runs one hygiene pass. Idempotent: only items that actually cross a
    /// transition boundary are written back, so re-running changes nothing
    /// (AC2). Returns the counts for observability.
    /// </summary>
    Task<HygieneResult> RunOnceAsync(CancellationToken cancellationToken = default);
}
