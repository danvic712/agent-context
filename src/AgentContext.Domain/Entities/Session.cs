namespace AgentContext.Domain.Entities;

/// <summary>
/// A record of one agent interaction, reported by the agent itself over MCP as a
/// structured summary (task, conclusion, key snippets), including model and token
/// usage (CONTEXT.md). Full original context is stored only when the user
/// explicitly asks to remember.
/// </summary>
public sealed class Session
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid WorkspaceId { get; set; }

    public Guid? DomainId { get; set; }

    /// <summary>Reporting agent instance (e.g. "craft-agents").</summary>
    public string AgentName { get; set; } = string.Empty;

    /// <summary>What the conversation set out to do (structured summary).</summary>
    public string Task { get; set; } = string.Empty;

    /// <summary>Outcome / conclusion of the conversation.</summary>
    public string Conclusion { get; set; } = string.Empty;

    /// <summary>Structured summary payload reported by the agent (JSON document).</summary>
    public string SummaryJson { get; set; } = "{}";

    /// <summary>Skill identifiers reported as used during the Session (JSON array).</summary>
    public string SkillsUsed { get; set; } = "[]";

    public SessionStatus Status { get; set; } = SessionStatus.Pending;

    /// <summary>True when the user explicitly asked to remember the full context.</summary>
    public bool Remembered { get; set; }

    /// <summary>Full original context, stored only when Remembered.</summary>
    public string? FullContext { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? ProcessedAtUtc { get; set; }

    /// <summary>Earliest time the worker may retry a Failed session.</summary>
    public DateTimeOffset? NextAttemptAtUtc { get; set; }

    public int ErrorCount { get; set; }
    public string? LastError { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public Domain? Domain { get; set; }
    public List<Usage> Usage { get; set; } = [];
    public List<Knowledge> Knowledge { get; set; } = [];
}
