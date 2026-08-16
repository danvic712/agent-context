using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using AgentContext.Application.Learning;
using AgentContext.Application.Settings;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using AgentContext.Tests.Fakes;
using AgentContext.Tests.Testcontainers;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Protocol;
using Pgvector;
using DomainEntity = AgentContext.Domain.Entities.Domain;

namespace AgentContext.Tests.AdapterTests;

/// <summary>
/// Secondary seam — retrieval over MCP (issue #5 AC1/AC2): the real --mcp-stdio
/// process is driven through the SDK's in-process client against the test
/// database. Embedding is served by a local HTTP stub the process reaches via
/// its DB-backed LLM settings (ADR 0003).
/// </summary>
public sealed class McpKnowledgeToolsTests : PostgresTestBase
{
    [Fact]
    public async Task Search_memory_tool_returns_domain_scoped_knowledge()
    {
        using var stub = new EmbeddingStub();

        await using (var db = Fixture.CreateDbContext())
        {
            await db.Database.MigrateAsync();
            var workspace = new Workspace { Name = "W", Type = WorkspaceType.Personal };
            var domain = new DomainEntity { WorkspaceId = workspace.Id, Name = "dev", IsShared = false };
            db.Workspaces.Add(workspace);
            db.Domains.Add(domain);
            db.Knowledge.Add(new Knowledge
            {
                WorkspaceId = workspace.Id,
                DomainId = domain.Id,
                Type = KnowledgeType.Solution,
                Title = "Title alpha",
                Content = "alpha",
                Confidence = 0.8,
                Embedding = new Vector(FakeLlmClient.VectorFor("alpha")),
                Status = KnowledgeStatus.Active,
            });
            await db.SaveChangesAsync();
            await new SettingsAppService(db).SaveLlmOptionsAsync(new LlmOptions
            {
                BaseUrl = stub.BaseUrl,
                ApiKey = "test-key",
                Model = "stub-model",
                EmbeddingModel = "text-embedding-3-small",
            });
        }

        await using var client = await McpProcess.CreateClientAsync(Fixture.ConnectionString);

        var tools = await client.ListToolsAsync();
        var searchMemory = Assert.Single(tools, t => t.Name == "search_memory");

        var result = await client.CallToolAsync(
            searchMemory.Name,
            new Dictionary<string, object?>
            {
                ["domain"] = "dev",
                ["query"] = "alpha",
            });

        var text = Assert.Single(result.Content.OfType<TextContentBlock>()).Text;
        using var doc = JsonDocument.Parse(text);
        var items = doc.RootElement.GetProperty("items");
        Assert.NotEqual(0, items.GetArrayLength());
        Assert.Equal("alpha", items[0].GetProperty("content").GetString());
        Assert.True(items[0].GetProperty("score").GetDouble() > 0.9);
    }

    /// <summary>
    /// Minimal OpenAI-compatible /embeddings stub: returns the deterministic
    /// FakeLlmClient.VectorFor(text) so the stdio process and the seeded Knowledge
    /// agree on embeddings without a real model.
    /// </summary>
    private sealed class EmbeddingStub : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly CancellationTokenSource _cts = new();

        public EmbeddingStub()
        {
            _listener.Prefixes.Add($"http://127.0.0.1:{GetFreePort()}/");
            _listener.Start();
            BaseUrl = _listener.Prefixes.Single().TrimEnd('/') + "/v1";
            _ = Task.Run(AcceptLoopAsync);
        }

        public string BaseUrl { get; }

        private async Task AcceptLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleAsync(context));
                }
                catch (HttpListenerException)
                {
                    break;
                }
            }
        }

        private async Task HandleAsync(HttpListenerContext context)
        {
            try
            {
                using var reader = new StreamReader(context.Request.InputStream, Encoding.UTF8);
                var body = await reader.ReadToEndAsync();
                var input = JsonDocument.Parse(body).RootElement.GetProperty("input")[0].GetString() ?? string.Empty;
                var vector = FakeLlmClient.VectorFor(input);

                var response = JsonSerializer.Serialize(new
                {
                    @object = "list",
                    data = new[] { new { @object = "embedding", index = 0, embedding = vector } },
                    model = "text-embedding-3-small",
                    usage = new { prompt_tokens = 1, total_tokens = 1 },
                });
                var bytes = Encoding.UTF8.GetBytes(response);

                context.Response.StatusCode = 200;
                context.Response.ContentType = "application/json";
                context.Response.ContentLength64 = bytes.Length;
                await context.Response.OutputStream.WriteAsync(bytes);
            }
            catch (Exception)
            {
                // Request aborted; nothing to do.
            }
            finally
            {
                context.Response.Close();
            }
        }

        public void Dispose()
        {
            _cts.Cancel();
            _listener.Close();
        }

        private static int GetFreePort()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            var port = ((IPEndPoint)listener.LocalEndpoint).Port;
            listener.Stop();
            return port;
        }
    }
}
