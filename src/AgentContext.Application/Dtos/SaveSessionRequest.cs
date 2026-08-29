namespace AgentContext.Application.Dtos;

/// <summary>
/// Input for recording a Session (spec §6.1 save_session): domain tag, structured
/// summary (task/conclusion/key snippets), reported Skills, optional remember + full
/// context, and an optional reported Usage payload. Shared by the REST endpoint and
/// the MCP save_session tool.
/// </summary>
public sealed record SaveSessionRequest(
    string? Domain,
    string Task,
    string Conclusion,
    IReadOnlyList<string>? KeySnippets = null,
    bool Remembered = false,
    string? FullContext = null,
    string? AgentName = null,
    SessionUsageInput? Usage = null,
    IReadOnlyList<string>? SkillsUsed = null);
