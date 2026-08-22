using AgentContext.Domain;

namespace AgentContext.Domain.Entities;

/// <summary>
/// A source-aware token usage ledger row. Reported Session usage and Learning
/// Engine platform usage share the token shape but keep their origins explicit.
/// </summary>
public sealed class Usage
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Optional for platform calls that are not tied to a Session.</summary>
    public Guid? SessionId { get; set; }

    public string Model { get; set; } = string.Empty;

    public int InputTokens { get; set; }
    /// <summary>A subset of <see cref="InputTokens"/>, never an additional total.</summary>
    public int CachedInputTokens { get; set; }
    public int OutputTokens { get; set; }

    public UsageSource Source { get; set; } = UsageSource.ReportedSession;

    /// <summary>
    /// Actual platform route used by Learning Engine calls, when known. The
    /// nullable relationship is set to null if a route is replaced or deleted,
    /// preserving the usage ledger row.
    /// </summary>
    public Guid? InferenceRouteId { get; set; }

    /// <summary>Capability used by a platform call; null for reported Session usage.</summary>
    public InferenceCapability? Capability { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Session? Session { get; set; }
    public InferenceRoute? InferenceRoute { get; set; }
}
