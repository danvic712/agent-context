using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using Microsoft.Extensions.Logging;

namespace AgentContext.Application.Learning;

/// <summary>
/// Writes source-aware Learning Engine usage rows. A provider response without
/// usable token metadata is intentionally observable but does not create a row.
/// </summary>
public sealed class LearningUsageRecorder(
    AgentContextDbContext db,
    ILogger<LearningUsageRecorder> logger) : ILearningUsageRecorder
{
    public async Task RecordAsync<T>(
        Guid? sessionId,
        InferenceCapability capability,
        LlmCallResult<T> result,
        CancellationToken cancellationToken = default)
    {
        if (result.Usage is null)
        {
            logger.LogWarning(
                "Learning Engine {Capability} call for session {SessionId} returned no usable usage metadata; " +
                "functional processing continues without a fabricated usage row.",
                capability,
                sessionId?.ToString() ?? "(none)");
            return;
        }

        db.Usage.Add(new Usage
        {
            SessionId = sessionId,
            Model = result.Model,
            InputTokens = result.Usage.InputTokens,
            CachedInputTokens = result.Usage.CachedInputTokens,
            OutputTokens = result.Usage.OutputTokens,
            Source = UsageSource.LearningEngine,
            InferenceRouteId = result.InferenceRouteId,
            Capability = capability,
        });

        // Persist each successful provider call immediately. If a later embedding
        // or knowledge operation fails, the usage already consumed is still auditable.
        await db.SaveChangesAsync(cancellationToken);
    }
}
