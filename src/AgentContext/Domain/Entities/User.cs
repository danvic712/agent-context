namespace AgentContext.Domain.Entities;

/// <summary>
/// A human account. Auth approach is flagged pending in the spec (Q23); the
/// first-run wizard creates the admin User. PasswordHash is stored with a
/// self-describing PBKDF2 format so a later auth implementation can verify it.
/// </summary>
public sealed class User
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    /// <summary>Format: pbkdf2-sha256$&lt;iterations&gt;$&lt;salt-b64&gt;$&lt;hash-b64&gt;.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public List<Membership> Memberships { get; set; } = [];
}
