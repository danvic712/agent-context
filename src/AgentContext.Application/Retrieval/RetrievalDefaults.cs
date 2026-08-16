namespace AgentContext.Application.Retrieval;

/// <summary>Retrieval tuning (spec §6.3 / issue #5): Confidence threshold and result cap.</summary>
public static class RetrievalDefaults
{
    /// <summary>Default retrieval Confidence threshold; sub-threshold items are excluded (AC3).</summary>
    public const double MinConfidence = 0.5;

    /// <summary>Maximum ranked items returned by search_memory.</summary>
    public const int TopN = 10;
}
