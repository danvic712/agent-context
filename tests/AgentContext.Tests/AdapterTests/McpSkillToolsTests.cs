using System.Text.Json;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using AgentContext.Tests.Testcontainers;
using Microsoft.EntityFrameworkCore;
using ModelContextProtocol.Protocol;
using DomainEntity = AgentContext.Domain.Entities.Domain;

namespace AgentContext.Tests.AdapterTests;

/// <summary>
/// Secondary seam — Skill management over MCP (T6, issue #7): drives the real
/// --mcp-stdio process through the SDK's in-process client. get_skill is pure DB
/// work (no LLM calls), so no embedding stub is needed. AC3 (get_skill over MCP)
/// and AC4 (skill://{domain}/{slug} resource) both land here.
/// </summary>
public sealed class McpSkillToolsTests : PostgresTestBase
{
    private async Task SeedSkillAsync()
    {
        await using var db = Fixture.CreateDbContext();
        await db.Database.MigrateAsync();
        var workspace = new Workspace { Name = "W", Type = WorkspaceType.Personal };
        var domain = new DomainEntity { WorkspaceId = workspace.Id, Name = "dev", IsShared = false };
        db.Workspaces.Add(workspace);
        db.Domains.Add(domain);
        db.Skills.Add(new Skill
        {
            WorkspaceId = workspace.Id,
            DomainId = domain.Id,
            Slug = "coding-guide",
            Name = "Coding Guide",
            Description = "Repo conventions",
            Instructions = "# Guide v1",
            Version = 1,
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Get_skill_tool_returns_the_latest_version()
    {
        await SeedSkillAsync();
        await using var client = await McpProcess.CreateClientAsync(Fixture.ConnectionString);

        var tools = await client.ListToolsAsync();
        var getSkill = Assert.Single(tools, t => t.Name == "get_skill");

        var result = await client.CallToolAsync(getSkill.Name, new Dictionary<string, object?>
        {
            ["domain"] = "dev",
            ["slug"] = "coding-guide",
        });

        var text = Assert.Single(result.Content.OfType<TextContentBlock>()).Text;
        using var doc = JsonDocument.Parse(text);
        Assert.Equal("dev", doc.RootElement.GetProperty("domainName").GetString());
        Assert.Equal("coding-guide", doc.RootElement.GetProperty("slug").GetString());
        Assert.Equal("# Guide v1", doc.RootElement.GetProperty("instructions").GetString());
        Assert.Equal(1, doc.RootElement.GetProperty("version").GetInt32());
    }

    [Fact]
    public async Task Skill_resource_resolves_to_the_latest_markdown()
    {
        await SeedSkillAsync();
        await using var client = await McpProcess.CreateClientAsync(Fixture.ConnectionString);

        // AC4: skill://{domain}/{slug} resource is advertised and readable.
        var templates = await client.ListResourceTemplatesAsync();
        var template = Assert.Single(templates, t => t.UriTemplate == "skill://{domain}/{slug}");

        var result = await client.ReadResourceAsync(template.UriTemplate, new Dictionary<string, object?>
        {
            ["domain"] = "dev",
            ["slug"] = "coding-guide",
        });

        var content = Assert.Single(result.Contents);
        var text = Assert.IsType<TextResourceContents>(content);
        Assert.Equal("skill://dev/coding-guide", text.Uri);
        Assert.Equal("text/markdown", text.MimeType);
        Assert.Equal("# Guide v1", text.Text);
    }
}
