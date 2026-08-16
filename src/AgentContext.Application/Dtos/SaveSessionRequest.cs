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
