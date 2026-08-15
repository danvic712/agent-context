namespace AgentContext.Domain.Entities;

/// <summary>
/// A container for users and their shared knowledge (CONTEXT.md). Personal and
/// Family workspaces come first; Team is a planned extension. Workspaces are the
/// tenancy seam where a future SaaS layer can be added (ADR 0002).
/// </summary>
public sealed class Workspace
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Name { get; set; } = string.Empty;

    public WorkspaceType Type { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    // Navigation properties
    public List<Domain> Domains { get; set; } = [];
    public List<Membership> Memberships { get; set; } = [];
    public List<Session> Sessions { get; set; } = [];
}
