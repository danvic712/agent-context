using ModelContextProtocol.Client;

namespace AgentContext.Tests.Testcontainers;

/// <summary>
/// Launches the real <c>--mcp-stdio</c> process for adapter smoke tests
/// (spec §Testing Decisions: MCP via the SDK's in-process client).
/// </summary>
public static class McpProcess
{
    /// <summary>Absolute path of the built dual-mode host binary, copied into the test output by the csproj.</summary>
    public static string AppBinaryPath
    {
        get
        {
            var path = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "AppBin", "AgentContext.Host"));
            Assert.True(File.Exists(path), $"App binary not found at {path}. Build the main project first.");
            return path;
        }
    }

    /// <summary>Creates an SDK MCP client over stdio, pointed at the test database.</summary>
    public static Task<McpClient> CreateClientAsync(string connectionString)
        => McpClient.CreateAsync(new StdioClientTransport(new StdioClientTransportOptions
        {
            Name = "agent-context-tests",
            Command = AppBinaryPath,
            Arguments = ["--mcp-stdio"],
            EnvironmentVariables = new Dictionary<string, string>
            {
                ["ConnectionStrings__Default"] = connectionString,
            },
        }));
}
