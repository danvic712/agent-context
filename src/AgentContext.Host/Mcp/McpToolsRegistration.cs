using AgentContext.Host.Mcp;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;

namespace AgentContext.Host.Mcp;

/// <summary>
/// Registration of the v1 MCP toolset (spec §6.1) plus the <c>skill://</c>
/// resources for the portal's Streamable HTTP endpoint (<c>/mcp</c>).
/// </summary>
public static class McpToolsRegistration
{
    public static IMcpServerBuilder AddAgentContextMcp(this IServiceCollection services)
    {
        var version = typeof(McpToolsRegistration).Assembly.GetName().Version?.ToString() ?? "0.1.0";
        return services.AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation
                {
                    Name = "agent-context",
                    Version = version,
                };
            })
            .WithTools<SessionTools>()
            .WithTools<KnowledgeTools>()
            .WithTools<SkillTools>()
            .WithResources<SkillResources>();
    }
}
