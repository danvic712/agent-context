namespace AgentContext.Application.Dtos;

/// <summary>Compact session list item with Usage rollups.</summary>
public sealed record SessionListItem(
    Guid Id,
    string? DomainName,
    string Task,
    string Status,
    bool Remembered,
    DateTimeOffset CreatedAtUtc,
    int TotalTokens,
    decimal TotalCost);
