namespace AgentContext.Domain.Entities;

/// <summary>Joins a User to a Workspace with a role (Workspace → Membership → User).</summary>
public sealed class Membership
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid WorkspaceId { get; set; }

    public Guid UserId { get; set; }

    public MembershipRole Role { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Workspace Workspace { get; set; } = null!;
    public User User { get; set; } = null!;
}
