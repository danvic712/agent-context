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
