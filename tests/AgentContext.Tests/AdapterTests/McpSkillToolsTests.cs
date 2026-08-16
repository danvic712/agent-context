using System.Text;
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
/// Secondary seam — Skill packages over MCP (T6, issue #7 + T12): drives the real
/// --mcp-stdio process through the SDK's in-process client. get_skill returns the
/// package (manifest + file contents); both resource templates resolve — the legacy
/// skill://{domain}/{slug} (SKILL.md) and skill://{domain}/{slug}/{file}.
/// </summary>
public sealed class McpSkillToolsTests : PostgresTestBase
{
    private static string? _skillsDir;

    private async Task SeedSkillAsync()
    {
        // Each test gets its own skills directory (shared via the process env for
        // the MCP binary launched by McpProcess).
        _skillsDir = Path.Combine(Path.GetTempPath(), "agent-context-mcp-skills-" + Guid.NewGuid().ToString("N"));
        Environment.SetEnvironmentVariable("Skills__Directory", _skillsDir);

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
    public async Task Get_skill_tool_returns_the_package_manifest_and_files()
    {
        await SeedSkillAsync();
        await using var client = await McpProcess.CreateClientAsync(Fixture.ConnectionString, SkillsEnv());

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
        Assert.Equal(1, doc.RootElement.GetProperty("version").GetInt32());

        // Manifest lists SKILL.md; files carry its content (migrated lazily).
        var manifest = doc.RootElement.GetProperty("manifest");
        Assert.Contains(manifest.EnumerateArray(), f => f.GetProperty("path").GetString() == "SKILL.md");
        var files = doc.RootElement.GetProperty("files");
        var main = Assert.Single(files.EnumerateArray(), f => f.GetProperty("path").GetString() == "SKILL.md");
        Assert.Equal("# Guide v1", main.GetProperty("content").GetString());
        Assert.False(main.GetProperty("binary").GetBoolean());
    }

    [Fact]
    public async Task Skill_resource_resolves_to_the_latest_markdown()
    {
        await SeedSkillAsync();
        await using var client = await McpProcess.CreateClientAsync(Fixture.ConnectionString, SkillsEnv());

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

    [Fact]
    public async Task Skill_file_resource_resolves_individual_package_files()
    {
        await SeedSkillAsync();
        await using var client = await McpProcess.CreateClientAsync(Fixture.ConnectionString, SkillsEnv());

        var templates = await client.ListResourceTemplatesAsync();
        var template = Assert.Single(templates, t => t.UriTemplate == "skill://{domain}/{slug}/{file}");

        var result = await client.ReadResourceAsync(template.UriTemplate, new Dictionary<string, object?>
        {
            ["domain"] = "dev",
            ["slug"] = "coding-guide",
            ["file"] = "SKILL.md",
        });

        var content = Assert.Single(result.Contents);
        var text = Assert.IsType<TextResourceContents>(content);
        Assert.Equal("skill://dev/coding-guide/SKILL.md", text.Uri);
        Assert.Equal("# Guide v1", text.Text);
    }

    private static Dictionary<string, string> SkillsEnv()
        => new() { ["Skills__Directory"] = _skillsDir ?? string.Empty };
}
