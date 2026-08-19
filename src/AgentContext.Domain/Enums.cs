namespace AgentContext.Domain;

/// <summary>Kinds of Workspace (see CONTEXT.md: Personal and Family first; Team planned).</summary>
public enum WorkspaceType
{
    Personal = 0,
    Family = 1,
    Team = 2,
}

/// <summary>Role of a User inside a Workspace (via Membership).</summary>
public enum MembershipRole
{
    Admin = 0,
    Member = 1,
}

/// <summary>Lifecycle of a reported Session as it moves through the Learning Engine.</summary>
public enum SessionStatus
{
    /// <summary>Saved, waiting for the Learning Engine worker.</summary>
    Pending = 0,

    /// <summary>Currently being processed by the pipeline.</summary>
    Processing = 1,

    /// <summary>Pipeline finished successfully; Knowledge (if any) has been created.</summary>
    Completed = 2,

    /// <summary>Pipeline failed; eligible for retry via NextAttemptAtUtc.</summary>
    Failed = 3,
}

/// <summary>Shape of a Knowledge item distilled from a Session (see spec §6.3).</summary>
public enum KnowledgeType
{
    Problem = 0,
    Solution = 1,
    Pattern = 2,
}

/// <summary>Hygiene state of a Knowledge item (see spec: decay to review/archive).</summary>
public enum KnowledgeStatus
{
    Active = 0,
    Review = 1,
    Archived = 2,
}

/// <summary>The model capability served by an inference route.</summary>
public enum InferenceCapability
{
    Chat = 0,
    Embedding = 1,
}
