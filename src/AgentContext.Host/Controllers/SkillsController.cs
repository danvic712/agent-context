using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using Microsoft.AspNetCore.Mvc;

namespace AgentContext.Host.Controllers;

/// <summary>
/// Skill surface (T6, issue #7): CRUD over the latest version, publish-new-version,
/// and get_skill over REST (spec US21–23). Thin adapters over the application seam.
/// </summary>
[ApiController]
[Route("api/skills")]
public sealed class SkillsController(ISkillAppService skills) : ControllerBase
{
    /// <summary>Create a Skill at version 1 in the given domain (AC1).</summary>
    [HttpPost]
    public async Task<ActionResult<SkillDetail>> Create([FromBody] CreateSkillRequest request, CancellationToken cancellationToken)
    {
        var created = await skills.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>All Skills — the latest version of each (domain, slug), newest first.</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SkillListItem>>> List(CancellationToken cancellationToken)
        => Ok(await skills.ListAsync(cancellationToken));

    /// <summary>
    /// get_skill over REST (AC3): resolves the latest version of a skill by
    /// domain + slug, matching the MCP tool and the skill://{domain}/{slug} resource.
    /// </summary>
    [HttpGet("by-slug")]
    public async Task<ActionResult<SkillDetail>> GetBySlug(
        [FromQuery] string domain,
        [FromQuery] string slug,
        CancellationToken cancellationToken)
        => Ok(await skills.GetBySlugAsync(domain, slug, cancellationToken));

    /// <summary>The Skill row with the given id (list rows reference the latest version).</summary>
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SkillDetail>> Get(Guid id, CancellationToken cancellationToken)
        => Ok(await skills.GetAsync(id, cancellationToken));

    /// <summary>
    /// Publish a new version on top of the skill with the given id (AC2/AC3):
    /// the current row stays as history, the next version is created with Version+1.
    /// </summary>
    [HttpPost("{id:guid}/publish")]
    public async Task<ActionResult<SkillDetail>> Publish(
        Guid id, [FromBody] PublishSkillRequest request, CancellationToken cancellationToken)
        => Ok(await skills.PublishAsync(id, request, cancellationToken));

    /// <summary>Delete the skill — every version of its (domain, slug) (AC4).</summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await skills.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
