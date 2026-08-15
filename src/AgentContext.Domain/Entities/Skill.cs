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

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Workspace Workspace { get; set; } = null!;
    public Domain Domain { get; set; } = null!;
}
