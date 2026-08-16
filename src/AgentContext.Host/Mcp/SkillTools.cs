using System.ComponentModel;
using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using ModelContextProtocol.Server;

namespace AgentContext.Host.Mcp;

/// <summary>
/// The v1 toolset's get_skill tool (spec §6.1 / T6, issue #7 US23): loads the
/// latest published version of a centrally-managed Skill by (domain, slug) —
/// no local installation needed. Backed by the same ISkillAppService seam as the
/// REST surface; the skill://{domain}/{slug} resource (SkillResources) resolves
/// the same data for clients that prefer resources over tools.
/// </summary>
[McpServerToolType]
public sealed class SkillTools(ISkillAppService skills)
{
    [McpServerTool(Name = "get_skill")]
    [Description("Loads the latest version of a Skill by domain and slug. Skills are managed centrally by the platform instead of being installed per machine. Throws when the skill does not exist.")]
    public async Task<SkillDetail> GetSkill(
        [Description("Domain the skill lives in, e.g. \"dev\".")] string domain,
        [Description("Stable skill slug, e.g. \"coding-guide\".")] string slug,
        CancellationToken cancellationToken = default)
        => await skills.GetBySlugAsync(domain, slug, cancellationToken);
}
