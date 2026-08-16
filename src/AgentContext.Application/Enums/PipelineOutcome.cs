namespace AgentContext.Application.Enums;

/// <summary>Outcome of running the Learning Engine pipeline on a Session.</summary>
public enum PipelineOutcome
{
    /// <summary>No eligible Session was waiting (worker tick found nothing to do).</summary>
    Idle = 0,

    /// <summary>Session processed successfully (Knowledge may or may not have been created).</summary>
    Completed = 1,

    /// <summary>Processing threw; the Session was moved to Failed with retry state recorded.</summary>
    Failed = 2,

    /// <summary>The Session was already claimed/processed and is not eligible again.</summary>
    NotClaimed = 3,
}
