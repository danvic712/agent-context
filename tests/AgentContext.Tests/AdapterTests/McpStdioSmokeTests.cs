using System.Diagnostics;
using System.Text;
using System.Text.Json;
using AgentContext.Tests.Testcontainers;

namespace AgentContext.Tests.AdapterTests;

/// <summary>
/// Secondary seam — process-level smoke test for the mcp-stdio entrypoint
/// (T1 acceptance: "--mcp-stdio starts an MCP server over stdio and exits
/// cleanly"). Launches the real binary, performs the initialize handshake and a
/// tools/list round-trip over newline-delimited JSON-RPC, then closes stdin and
/// asserts a clean exit.
/// </summary>
public sealed class McpStdioSmokeTests
{
    private static Process StartStdioServer()
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = McpProcess.AppBinaryPath,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            Environment =
            {
                // The shared DI requires a connection string even though the
                // skeleton echo tool never touches the database.
                ["ConnectionStrings__Default"] = "Host=127.0.0.1;Port=1;Database=none;Username=none;Password=none",
            },
        };
        startInfo.ArgumentList.Add("--mcp-stdio");
        return Process.Start(startInfo)!;
    }

    private static async Task<string> ReadResponseLineAsync(StreamReader stdout, int id, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var line = await stdout.ReadLineAsync(cancellationToken);
            Assert.False(line is null, "MCP server closed stdout before responding.");
            using var doc = JsonDocument.Parse(line);
            if (doc.RootElement.TryGetProperty("id", out var idProp) &&
                idProp.ValueKind == JsonValueKind.Number &&
                idProp.GetInt32() == id)
            {
                return line;
            }
        }

        throw new TimeoutException("Timed out waiting for the MCP server response.");
    }

    private static async Task<(Process Process, StreamReader Stdout, StreamWriter Stdin)> StartServerAsync()
    {
        var process = StartStdioServer();
        // Drain stderr to avoid blocking the child on a full pipe; failures surface in exit assertions.
        _ = process.StandardError.ReadToEndAsync();
        return (process, process.StandardOutput, process.StandardInput);
    }

    [Fact]
    public async Task Stdio_server_handshakes_exposes_tools_and_exits_cleanly()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var (process, stdout, stdin) = await StartServerAsync();

        try
        {
            // 1. initialize handshake
            await stdin.WriteLineAsync(
                """{"jsonrpc":"2.0","id":1,"method":"initialize","params":{"protocolVersion":"2025-06-18","capabilities":{},"clientInfo":{"name":"smoke","version":"1.0"}}}""");
            await stdin.FlushAsync();

            var initResponse = await ReadResponseLineAsync(stdout, 1, cts.Token);
            using (var doc = JsonDocument.Parse(initResponse))
            {
                Assert.True(doc.RootElement.TryGetProperty("result", out var result));
                Assert.Equal("agent-context", result.GetProperty("serverInfo").GetProperty("name").GetString());
                Assert.True(result.GetProperty("capabilities").TryGetProperty("tools", out _));
            }

            // 2. notify initialized, then list tools
            await stdin.WriteLineAsync("""{"jsonrpc":"2.0","method":"notifications/initialized"}""");
            await stdin.WriteLineAsync("""{"jsonrpc":"2.0","id":2,"method":"tools/list"}""");
            await stdin.FlushAsync();

            var toolsResponse = await ReadResponseLineAsync(stdout, 2, cts.Token);
            using (var doc = JsonDocument.Parse(toolsResponse))
            {
                var tools = doc.RootElement.GetProperty("result").GetProperty("tools");
                var names = tools.EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToList();
                Assert.Contains("echo", names);
            }

            // 3. closing stdin ends the server → clean exit
            stdin.Close();
            Assert.True(process.WaitForExit(10_000), "MCP server did not exit after stdin closed.");
            Assert.Equal(0, process.ExitCode);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }

            process.Dispose();
        }
    }
}
