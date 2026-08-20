namespace AgentContext.Domain.Entities;

/// <summary>
/// A reusable capability unit an agent can load, managed centrally instead of
/// installed per machine (CONTEXT.md). A skill = markdown instructions + version + domain.
/// </summary>
public sealed class Skill
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid WorkspaceId { get; set; }

    public Guid DomainId { get; set; }

    /// <summary>Stable identifier used by MCP get_skill (skill://{domain}/{slug}).</summary>
    public string Slug { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Markdown instructions the agent loads.</summary>
    public string Instructions { get; set; } = string.Empty;

    /// <summary>Monotonically increasing per (domain, slug); publish bumps it.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Id of the version this Skill was created from, when applicable.</summary>
    public Guid? PreviousVersionId { get; set; }

    /// <summary>How this Skill package entered the platform.</summary>
    public string? SourceType { get; set; }

    /// <summary>Remote source URL for catalog-backed Skills.</summary>
    public string? SourceUrl { get; set; }

    /// <summary>Remote source revision captured at download time.</summary>
    public string? SourceRevision { get; set; }

    /// <summary>Deterministic package digest captured at download time.</summary>
    public string? SourceDigest { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Workspace Workspace { get; set; } = null!;
    public Domain Domain { get; set; } = null!;
    public Skill? PreviousVersion { get; set; }
    public List<Skill> NextVersions { get; set; } = [];
}
