using System.ComponentModel.DataAnnotations.Schema;
using Pgvector;

namespace AgentContext.Domain.Entities;

/// <summary>
/// A reusable answer, pattern, or learning distilled automatically from session
/// summaries, stored with a confidence score and retrievable within its domain
/// (CONTEXT.md). Embedding lives in pgvector for semantic retrieval.
/// </summary>
public sealed class Knowledge
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid WorkspaceId { get; set; }

    public Guid DomainId { get; set; }

    public KnowledgeType Type { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    /// <summary>0..1 — see spec §6.3 for how Confidence is computed and adjusted.</summary>
    public double Confidence { get; set; }

    /// <summary>Per-item private marker, overriding domain-level visibility.</summary>
    public bool IsPrivate { get; set; }

    /// <summary>Session this Knowledge was distilled from.</summary>
    public Guid? SourceSessionId { get; set; }

    /// <summary>Group id shared by a pair of conflicting Knowledge items (retrieval shows both).</summary>
    public string? ConflictGroupId { get; set; }

    /// <summary>
    /// pgvector embedding for semantic search (nullable until the pipeline embeds it).
    /// Dimension 1536 matches the default OpenAI embedding model; migrate if the
    /// configured endpoint requires another size.
    /// </summary>
    [Column(TypeName = "vector(1536)")]
    public Vector? Embedding { get; set; }

    public KnowledgeStatus Status { get; set; } = KnowledgeStatus.Active;

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? LastUsedAtUtc { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public Domain Domain { get; set; } = null!;
    public Session? SourceSession { get; set; }
}
