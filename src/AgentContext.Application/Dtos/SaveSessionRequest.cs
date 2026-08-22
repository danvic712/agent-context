namespace AgentContext.Application.Dtos;

/// <summary>
/// Input for recording a Session (spec §6.1 save_session): domain tag, structured
/// summary (task/conclusion/key snippets), optional remember + full context, and
/// model and token fields which may be recorded in the source-aware Usage ledger.
/// Cost remains a legacy request field until the reported-usage contract is updated
/// by T27; it is not persisted. Shared by the REST endpoint and the MCP save_session
/// tool.
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
