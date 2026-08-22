using AgentContext.Application.Dtos;
using AgentContext.Domain;

namespace AgentContext.Application.Contracts;

/// <summary>Persists usage consumed by one Learning Engine provider call.</summary>
public interface ILearningUsageRecorder
{
    Task RecordAsync<T>(
        Guid? sessionId,
        InferenceCapability capability,
        LlmCallResult<T> result,
        CancellationToken cancellationToken = default);
}
