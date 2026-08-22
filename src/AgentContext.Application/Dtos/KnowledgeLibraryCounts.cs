namespace AgentContext.Application.Dtos;

/// <summary>Unfiltered counts for the three Knowledge lifecycle states.</summary>
public sealed record KnowledgeLibraryCounts(
    int Active,
    int Review,
    int Archived);
