using System.ComponentModel;
using AgentContext.Application.Contracts;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AgentContext.Host.Mcp;

/// <summary>
/// The resource surface for Skill packages (spec §6.1 / T12): skill://{domain}/{slug}
/// resolves the main SKILL.md (compat with pre-package skills), and the extended
/// skill://{domain}/{slug}/{file} template resolves any single package file —
/// text files as markdown/text, binary files as base64 blobs. File paths with
/// subdirectories arrive URL-encoded (the {file} template parameter is a single
/// segment).
/// </summary>
[McpServerResourceType]
public sealed class SkillResources(
    ISkillAppService skills,
    ISettingsAppService settings,
    ILocalesAppService locales)
{
    [McpServerResource(UriTemplate = "skill://{domain}/{slug}", Name = "Skill", MimeType = "text/markdown")]
    [Description("The latest version of a Skill's main instructions file (SKILL.md), resolved by domain and slug.")]
    public async Task<ResourceContents> GetSkill(
        string domain,
        string slug,
        CancellationToken cancellationToken)
    {
        var skill = await skills.GetBySlugAsync(domain, slug, cancellationToken);
        return new TextResourceContents
        {
            Uri = $"skill://{domain}/{slug}",
            MimeType = "text/markdown",
            Text = System.Text.Encoding.UTF8.GetString(await skills.ReadFileAsync(skill.Id, "SKILL.md", cancellationToken)),
        };
    }

    [McpServerResource(UriTemplate = "skill://{domain}/{slug}/{file}", Name = "Skill file", MimeType = "*/*")]
    [Description("A single file inside the latest version of a Skill package, resolved by domain, slug and file path (subdirectory paths are URL-encoded, e.g. %2F for /).")]
    public async Task<ResourceContents> GetSkillFile(
        string domain,
        string slug,
        string file,
        CancellationToken cancellationToken)
    {
        var skill = await skills.GetBySlugAsync(domain, slug, cancellationToken);
        var path = Uri.UnescapeDataString(file);
        var content = await skills.ReadFileAsync(skill.Id, path, cancellationToken);

        // Text files come back as text contents; binary files as base64 blobs.
        return IsBinary(content)
            ? new BlobResourceContents
            {
                Uri = $"skill://{domain}/{slug}/{file}",
                MimeType = "application/octet-stream",
                Blob = content,
            }
            : new TextResourceContents
            {
                Uri = $"skill://{domain}/{slug}/{file}",
                MimeType = "text/plain; charset=utf-8",
                Text = System.Text.Encoding.UTF8.GetString(content),
            };
    }

    private static bool IsBinary(byte[] content)
    {
        var sample = Math.Min(content.Length, 512);
        for (var i = 0; i < sample; i++)
        {
            if (content[i] == 0)
            {
                return true;
            }
        }

        return false;
    }
}
