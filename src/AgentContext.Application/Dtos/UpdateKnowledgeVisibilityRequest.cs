namespace AgentContext.Application.Dtos;

/// <summary>PATCH /api/knowledge/{id} body: toggles the per-item private marker.</summary>
public sealed record UpdateKnowledgeVisibilityRequest(bool IsPrivate);
