namespace AgentContext.Application.Dtos;

/// <summary>
/// Input for recording a Session (spec §6.1 save_session): domain tag, structured
/// summary (task/conclusion/key snippets), optional remember + full context, and
/// model/tokens/cost which land in Usage. Shared by the REST endpoint and the MCP
/// save_session tool.
/// </summary>
public sealed record SaveSessionRequest(
    string? Domain,
    string Task,
    string Conclusion,
    IReadOnlyList<string>? KeySnippets = null,
    bool Remembered = false,
    string? FullContext = null,
    string? AgentName = null,
    string? Model = null,
    int TokensIn = 0,
    int TokensOut = 0,
    decimal? Cost = null);

/// <summary>Outcome of a successful save_session call.</summary>
public sealed record SaveSessionResult(Guid SessionId, string? DomainName, bool Remembered);

/// <summary>One Usage row exposed on a session detail.</summary>
public sealed record SessionUsageDto(string Model, int TokensIn, int TokensOut, decimal Cost);

/// <summary>Full session view including Usage (spec §6.2: overview data queryable from Usage).</summary>
public sealed record SessionDetail(
    Guid Id,
    Guid WorkspaceId,
    string? DomainName,
    string AgentName,
    string Task,
    string Conclusion,
    string SummaryJson,
    string Status,
    bool Remembered,
    DateTimeOffset CreatedAtUtc,
    IReadOnlyList<SessionUsageDto> Usage);

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
