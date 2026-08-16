namespace AgentContext.Application.Dtos;

/// <summary>Outcome of rate_knowledge: the item's adjusted Confidence.</summary>
public sealed record RateKnowledgeResult(Guid Id, double Confidence);
