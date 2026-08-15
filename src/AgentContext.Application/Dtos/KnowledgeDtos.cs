using AgentContext.Domain;

namespace AgentContext.Application.Dtos;

/// <summary>
/// One Knowledge item extracted from a Session summary by the LLM
/// (spec §6.3; extraction output schema confirmed in T3): a Problem / Solution /
/// Pattern with a self-assessment of how confident the model is that the item
/// is correct and useful (0..1).
/// </summary>
public sealed record KnowledgeExtraction(
    KnowledgeType Type,
    string Title,
    string Content,
    double SelfAssessment);

/// <summary>Outcome of running the Learning Engine pipeline on a Session.</summary>
public enum PipelineOutcome
{
    /// <summary>No eligible Session was waiting (worker tick found nothing to do).</summary>
    Idle = 0,

    /// <summary>Session processed successfully (Knowledge may or may not have been created).</summary>
    Completed = 1,

    /// <summary>Processing threw; the Session was moved to Failed with retry state recorded.</summary>
    Failed = 2,

    /// <summary>The Session was already claimed/processed and is not eligible again.</summary>
    NotClaimed = 3,
}

/// <summary>Result of <c>ILearningPipelineAppService.ProcessAsync</c> / <c>ProcessNextAsync</c>.</summary>
public sealed record LearningPipelineResult(
    Guid? SessionId,
    PipelineOutcome Outcome,
    int KnowledgeCreated = 0,
    int KnowledgeCorroborated = 0,
    string? Error = null);
