using System.ComponentModel;
using ModelContextProtocol.Server;

namespace AgentContext.Mcp;

/// <summary>
/// T1 skeleton placeholder tool proving the stdio MCP loop (list_tools → call_tool).
/// The v1 toolset — save_session, search_memory, find_similar_solution, get_skill,
/// rate_knowledge (spec §6.1) — lands with tickets T2–T6.
/// </summary>
[McpServerToolType]
public sealed class SkeletonTools
{
    [McpServerTool]
    [Description("Echoes the message back. Skeleton placeholder for the T1 stdio MCP loop.")]
    public string Echo(string message) => $"echo: {message}";
}
