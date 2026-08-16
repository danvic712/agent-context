namespace AgentContext.Application.Dtos;

/// <summary>Ranked search_memory results (conflict partners included side by side).</summary>
public sealed record SearchMemoryResult(IReadOnlyList<KnowledgeSearchItem> Items);
