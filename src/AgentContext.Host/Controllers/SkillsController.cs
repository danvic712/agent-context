using System.Net;
using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Application.Localization;
using Microsoft.AspNetCore.Mvc;

namespace AgentContext.Host.Controllers;

/// <summary>Uploaded Skill package creation and read-only package access.</summary>
[ApiController]
[Route("api/skills")]
public sealed class SkillsController(ISkillAppService skills) : ControllerBase
{
    /// <summary>Creates a new Skill from an uploaded ZIP package or single file (T18).</summary>
    [HttpPost("upload")]
    public async Task<ActionResult<SkillDetail>> Upload(
        [FromForm] string domain,
        [FromForm] string slug,
        [FromForm] string name,
        [FromForm] string description,
        [FromForm] string? kind,
        [FromForm] IFormFile? archive,
        CancellationToken cancellationToken)
    {
        if (archive is null || archive.Length == 0)
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.ImportInvalid);
        }

        var uploadKind = string.IsNullOrWhiteSpace(kind)
            ? (Path.GetExtension(archive.FileName).Equals(".zip", StringComparison.OrdinalIgnoreCase) ? "zip" : "file")
            : kind.Trim().ToLowerInvariant();

        using var stream = archive.OpenReadStream();
        SkillDetail created;
        if (uploadKind == "file")
        {
            created = await skills.CreateFromFileAsync(
                new CreateSkillFromFileRequest(domain, slug, name, description, archive.FileName),
                stream,
                cancellationToken);
        }
        else if (uploadKind == "zip")
        {
            created = await skills.CreateFromZipAsync(
                new CreateSkillFromZipRequest(domain, slug, name, description),
                stream,
                cancellationToken);
        }
        else
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Skill.ImportInvalid);
        }

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
        [FromQuery] string? search,
        [FromQuery] string? domain,
        [FromQuery] string? sourceType,
        [FromQuery] string? sort,
        CancellationToken cancellationToken)
        => Ok(await skills.ListAsync(
            new SkillListQuery(pageSize, cursor, search, domain, sourceType, sort),
            cancellationToken));

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
    /// Read one package file (T12): raw bytes with a mime type derived from the
    /// file extension (binary assets included).
    /// </summary>
    [HttpGet("{id:guid}/file")]
    public async Task<IActionResult> ReadFile(Guid id, [FromQuery] string path, CancellationToken cancellationToken)
    {
        var content = await skills.ReadFileAsync(id, path, cancellationToken);
        return File(content, ContentType(path));
    }

    /// <summary>Downloads the persisted package as a ZIP archive.</summary>
    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken cancellationToken)
    {
        var package = await skills.DownloadPackageAsync(id, cancellationToken);
        return File(package.Content, "application/zip", package.FileName);
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
