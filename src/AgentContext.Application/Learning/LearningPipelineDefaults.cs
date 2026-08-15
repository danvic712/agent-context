namespace AgentContext.Application.Learning;

/// <summary>
/// Tuning constants for the Learning Engine pipeline (spec §6.3 + T3 handoff).
/// T3 implements the first two Confidence weights; corroboration/novelty hooks
/// land in T4/T5 (retrieval feedback). The retry schedule is the Postgres-as-queue
/// policy (ADR 0005) — configurable via settings in a later ticket.
/// </summary>
public static class LearningPipelineDefaults
{
    /// <summary>pgvector column dimension — baked into the migration as vector(1536).</summary>
    public const int EmbeddingDimensions = 1536;

    /// <summary>Confidence weight: extraction self-assessment (spec §6.3).</summary>
    public const double SelfAssessmentWeight = 0.4;

    /// <summary>Confidence weight: field completeness (spec §6.3).</summary>
    public const double CompletenessWeight = 0.2;

    /// <summary>Cap for T3: only the first two weights are live (max 0.4 + 0.2).</summary>
    public const double MaxT3Confidence = SelfAssessmentWeight + CompletenessWeight;

    /// <summary>Confidence bump when duplicate content corroborates existing Knowledge (AC3).</summary>
    public const double CorroborationBump = 0.05;

    /// <summary>Cosine similarity at/above which an item is treated as a duplicate.</summary>
    public const double DedupCosineThreshold = 0.9;

    /// <summary>Retry backoff base delay; doubles per attempt, capped.</summary>
    public static readonly TimeSpan RetryBaseDelay = TimeSpan.FromSeconds(30);

    /// <summary>Retry backoff cap.</summary>
    public static readonly TimeSpan RetryMaxDelay = TimeSpan.FromHours(1);

    /// <summary>
    /// Retry budget. After this many failures a Session stays Failed and visible
    /// (never deleted) with no further automatic retry scheduled.
    /// </summary>
    public const int MaxAttempts = 5;
}
