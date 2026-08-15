using System.Text.Json;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Tests.Testcontainers;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Protocol;
using DomainEntity = AgentContext.Domain.Entities.Domain;

namespace AgentContext.Tests.AdapterTests;

/// <summary>
/// Secondary seam — save_session over MCP (T2 AC: "Calling the same via the MCP
/// tool (in-process client test) creates the same row"). Uses the SDK's
/// in-process client over stdio against the real host binary and the test
/// database (spec §Testing Decisions).
/// </summary>
public sealed class McpSaveSessionTests : PostgresTestBase
{
    [Fact]
    public async Task Save_session_tool_creates_the_same_row_as_rest()
    {
        // Seed the first-run state (workspace + "dev" domain) directly.
        await using (var db = Fixture.CreateDbContext())
        {
            await db.Database.MigrateAsync();
            var workspace = new Workspace { Name = "Danvic's Workspace", Type = WorkspaceType.Personal };
            db.Workspaces.Add(workspace);
            db.Domains.Add(new DomainEntity { WorkspaceId = workspace.Id, Name = "dev", IsShared = false });
            await db.SaveChangesAsync();
        }

        await using var client = await McpProcess.CreateClientAsync(Fixture.ConnectionString);

        var tools = await client.ListToolsAsync();
        var saveSession = Assert.Single(tools, t => t.Name == "save_session");

        var result = await client.CallToolAsync(
            saveSession.Name,
            new Dictionary<string, object?>
            {
                ["domain"] = "dev",
                ["task"] = "MCP-reported task",
                ["conclusion"] = "MCP-reported conclusion",
                ["keySnippets"] = new[] { "snippet one" },
                ["model"] = "gpt-4o",
                ["tokensIn"] = 300,
                ["tokensOut"] = 100,
                ["cost"] = 0.05m,
            });

        var text = Assert.Single(result.Content.OfType<TextContentBlock>()).Text;
        using var doc = JsonDocument.Parse(text);
        var sessionId = doc.RootElement.GetProperty("sessionId").GetGuid();

        // The same row shape as the REST path: Session + Usage in the database.
        await using var checkDb = Fixture.CreateDbContext();
        var session = await checkDb.Sessions.Include(s => s.Domain).Include(s => s.Usage)
            .SingleAsync(s => s.Id == sessionId);
        Assert.Equal("dev", session.Domain!.Name);
        Assert.Equal("MCP-reported task", session.Task);

        var usage = Assert.Single(session.Usage);
        Assert.Equal("gpt-4o", usage.Model);
        Assert.Equal(300, usage.TokensIn);
        Assert.Equal(100, usage.TokensOut);
        Assert.Equal(0.05m, usage.Cost);
    }
}
