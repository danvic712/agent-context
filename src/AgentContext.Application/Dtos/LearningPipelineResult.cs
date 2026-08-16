using AgentContext.Application.Enums;

namespace AgentContext.Application.Dtos;

/// <summary>Result of <c>ILearningPipelineAppService.ProcessAsync</c> / <c>ProcessNextAsync</c>.</summary>
public sealed record LearningPipelineResult(
    Guid? SessionId,
    PipelineOutcome Outcome,
    int KnowledgeCreated = 0,
    int KnowledgeCorroborated = 0,
    string? Error = null);
