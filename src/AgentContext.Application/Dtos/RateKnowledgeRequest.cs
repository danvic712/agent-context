namespace AgentContext.Application.Dtos;

/// <summary>POST /api/knowledge/{id}/rate body: useful (confirm) or not (clear).</summary>
public sealed record RateKnowledgeRequest(bool Useful);
