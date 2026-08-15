namespace AgentContext.Domain.Entities;

/// <summary>
/// A classification of knowledge by area of life or work (e.g. dev, home, business).
/// Domains live inside a Workspace; knowledge does not flow across workspaces
/// automatically (CONTEXT.md).
/// </summary>
public sealed class Domain
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid WorkspaceId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Domain-level visibility: true = shared with the Workspace, false = private.</summary>
    public bool IsShared { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Workspace Workspace { get; set; } = null!;
    public List<Knowledge> Knowledge { get; set; } = [];
    public List<Skill> Skills { get; set; } = [];
}
