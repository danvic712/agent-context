using System.ComponentModel;
using AgentContext.Application.Contracts;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace AgentContext.Host.Mcp;

/// <summary>
/// The v1 resource surface for Skills (spec §6.1): skill://{domain}/{slug} resolves
/// to the latest published version's markdown instructions (AC4, issue #7).
/// </summary>
[McpServerResourceType]
public sealed class SkillResources(ISkillAppService skills)
{
    [McpServerResource(UriTemplate = "skill://{domain}/{slug}", Name = "Skill", MimeType = "text/markdown")]
    [Description("The latest version of a Skill's markdown instructions, resolved by domain and slug.")]
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
            Text = skill.Instructions,
        };
    }
}
