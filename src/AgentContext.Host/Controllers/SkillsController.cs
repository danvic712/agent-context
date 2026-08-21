using System.Net;
using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Application.Localization;
using Microsoft.AspNetCore.Mvc;

namespace AgentContext.Host.Controllers;

/// <summary>
    /// Skill surface (T6, issue #7 + T12 package model): CRUD over the latest
    /// version, publish-new-version, get_skill over REST, plus per-file operations
    /// (read / write / delete / bulk upload / zip import) against the filesystem
    /// package. Thin adapters over the application seam.
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

    /// <summary>Creates a new Skill from a multipart ZIP package (T18).</summary>
    [HttpPost("upload")]
    public async Task<ActionResult<SkillDetail>> Upload(
        [FromForm] string domain,
        [FromForm] string slug,
        [FromForm] string name,
        [FromForm] string description,
        [FromForm] IFormFile? archive,
        CancellationToken cancellationToken)
    {
        if (archive is null || archive.Length == 0)
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.ImportInvalid);
        }

        using var stream = archive.OpenReadStream();
        var created = await skills.CreateFromZipAsync(
            new CreateSkillFromZipRequest(domain, slug, name, description),
            stream,
            cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    /// <summary>
    /// Returns a bounded page of the latest version of each (domain, slug), newest
    /// first. The default page size is 20 and the maximum is 100. Pass the opaque
    /// response cursor as <c>cursor</c> to continue scrolling.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<SkillListPage>> List(
        [FromQuery] int? pageSize,
        [FromQuery] string? cursor,
        CancellationToken cancellationToken)
        => Ok(await skills.ListAsync(pageSize, cursor, cancellationToken));

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

    /// <summary>
    /// Read one package file (T12): raw bytes with a mime type derived from the
    /// file extension (binary assets included).
    /// </summary>
    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> ReadFile(Guid id, [FromQuery] string path, CancellationToken cancellationToken)
    {
        var content = await skills.ReadFileAsync(id, path, cancellationToken);
        return File(content, ContentType(path));
    }

    /// <summary>
    /// Write (create or overwrite) one package file (T12). The request body is the
    /// raw file content — text or binary.
    /// </summary>
    [HttpPut("{id:guid}/file")]
    public async Task<ActionResult<SkillDetail>> WriteFile(Guid id, [FromQuery] string path, CancellationToken cancellationToken)
    {
        using var buffer = new MemoryStream();
        await Request.Body.CopyToAsync(buffer, cancellationToken);
        return Ok(await skills.WriteFileAsync(id, path, buffer.ToArray(), cancellationToken));
    }

    /// <summary>Delete one package file (T12); empty parent directories are pruned.</summary>
    [HttpDelete("{id:guid}/file")]
    public async Task<ActionResult<SkillDetail>> DeleteFile(Guid id, [FromQuery] string path, CancellationToken cancellationToken)
        => Ok(await skills.DeleteFileAsync(id, path, cancellationToken));

    /// <summary>
    /// Bulk upload into the package (T12, drag-and-drop in the UI): each uploaded
    /// file lands at its relative name. Binary assets are supported.
    /// </summary>
    [HttpPost("{id:guid}/files")]
    public async Task<ActionResult<SkillDetail>> UploadFiles(Guid id, CancellationToken cancellationToken)
    {
        if (Request.Form.Files.Count == 0)
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.ImportInvalid);
        }

        SkillDetail detail = await skills.GetAsync(id, cancellationToken);
        foreach (var file in Request.Form.Files)
        {
            using var stream = file.OpenReadStream();
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken);
            detail = await skills.WriteFileAsync(id, file.FileName, buffer.ToArray(), cancellationToken);
        }

        return Ok(detail);
    }

    /// <summary>
    /// Import a package from a zip archive (T12): entries are extracted into the
    /// package; SKILL.md is created when the zip omits it.
    /// </summary>
    [HttpPost("{id:guid}/import")]
    public async Task<ActionResult<SkillDetail>> Import(Guid id, CancellationToken cancellationToken)
    {
        if (Request.Form.Files.Count == 0)
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.ImportInvalid);
        }

        using var stream = Request.Form.Files[0].OpenReadStream();
        return Ok(await skills.ImportZipAsync(id, stream, cancellationToken));
    }

    private static string ContentType(string path)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".md" or ".markdown" => "text/markdown; charset=utf-8",
            ".txt" => "text/plain; charset=utf-8",
            ".json" => "application/json",
            ".ts" or ".tsx" => "text/typescript",
            ".js" or ".mjs" => "text/javascript",
            ".css" => "text/css",
            ".html" or ".htm" => "text/html",
            ".sql" => "text/sql",
            ".sh" or ".bash" => "text/x-shellscript",
            ".svg" => "image/svg+xml",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            ".pdf" => "application/pdf",
            ".zip" => "application/zip",
            _ => "application/octet-stream",
        };
    }
}
