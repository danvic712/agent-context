using AgentContext.Application;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AgentContext.Host.Mcp;

/// <summary>
/// The <c>--mcp-stdio</c> entrypoint (ADR 0006): a single-session MCP server over
/// stdio for Craft Agents local sources. Shares the same DI graph and DbContext as
/// the web mode via <see cref="ServiceCollectionExtensions.AddApplicationServices"/>.
/// A generic Host is used so no HTTP port is bound in this mode.
/// </summary>
public static class McpStdioHost
{
    public static async Task<int> RunAsync(string[] args)
    {
        // Fully qualified: the AgentContext.Host namespace would shadow the Host type.
        var builder = Microsoft.Extensions.Hosting.Host.CreateApplicationBuilder(args);

        // All MCP protocol traffic goes over stdio; keep logs on stderr so they do
        // not corrupt the JSON-RPC stream.
        builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

        builder.Services.AddApplicationServices(builder.Configuration);

        builder.Services
            .AddMcpServer(options =>
            {
                options.ServerInfo = new Implementation
                {
                    Name = "agent-context",
                    Version = typeof(McpStdioHost).Assembly.GetName().Version?.ToString() ?? "0.1.0",
                };
            })
            .WithStdioServerTransport()
            .WithTools<SkeletonTools>();

        await builder.Build().RunAsync();
        return 0;
    }
}
