using System.ComponentModel;
using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using ModelContextProtocol.Server;

namespace AgentContext.Host.Mcp;

/// <summary>
/// The v1 toolset's get_skill tool (spec §6.1 / T6, issue #7 US23 + T12 package
/// model): loads the latest published version of a centrally-managed Skill by
/// (domain, slug) as a package — the metadata, the file manifest, and every
/// file's content (text files inline, binary files base64-encoded). No local
/// installation needed. Backed by the same ISkillAppService seam as the REST
/// surface; the skill://{domain}/{slug}/{file} resources (SkillResources) resolve
/// the same data for clients that prefer resources over tools. Errors are
/// localized (T11) through the shared translation service.
/// </summary>
[McpServerToolType]
public sealed class SkillTools(
    ISkillAppService skills,
    ISettingsAppService settings,
    ITranslationService translations)
{
    [McpServerTool(Name = "get_skill")]
    [Description("Loads the latest version of a Skill by domain and slug as a package: metadata plus the file manifest and every file's content (SKILL.md and any code examples, tools or assets). Binary files are base64-encoded with a binary flag. Throws when the skill does not exist.")]
    public async Task<SkillPackage> GetSkill(
        [Description("Domain the skill lives in, e.g. \"dev\".")] string domain,
        [Description("Stable skill slug, e.g. \"coding-guide\".")] string slug,
        CancellationToken cancellationToken = default)
        => await McpErrorLocalizer.ExecuteAsync(settings, translations, () =>
            skills.GetPackageAsync(domain, slug, cancellationToken), cancellationToken);
}
