using AgentContext.Application.Dtos;

namespace AgentContext.Application.Contracts;

/// <summary>
/// The Learning Engine pipeline (spec §6.3, issue #4): extraction → embedding →
/// dedup → pgvector, driving Session state as a Postgres-as-queue (ADR 0005).
/// Synchronously invocable (AC5): the BackgroundService is only a scheduler —
/// it calls <see cref="ProcessNextAsync"/>; tests call
/// <see cref="ProcessAsync"/> on a recorded Session through the same seam.
/// </summary>
public interface ILearningPipelineAppService
{
    /// <summary>
    /// Claims the next eligible Session (Pending, or Failed whose
    /// <c>NextAttemptAtUtc</c> has arrived) and processes it. Returns
    /// <see cref="PipelineOutcome.Idle"/> when nothing is waiting.
    /// </summary>
    Task<LearningPipelineResult> ProcessNextAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Claims and processes one specific Session. Primary test seam (AC5).
    /// Returns <see cref="PipelineOutcome.NotClaimed"/> when the Session is not
    /// eligible (already Completed, already claimed, or retry not yet due).
    /// </summary>
    Task<LearningPipelineResult> ProcessAsync(Guid sessionId, CancellationToken cancellationToken = default);
}
