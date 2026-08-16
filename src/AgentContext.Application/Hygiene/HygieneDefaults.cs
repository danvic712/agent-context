namespace AgentContext.Application.Hygiene;

/// <summary>
/// Knowledge hygiene tuning (T8 / spec US20 + §6.3 "temporal decay reduces
/// long-unused items"). Items untouched for a decay window lose Confidence; once
/// they fall below the retrieval threshold they move to Review, and Review items
/// left untouched beyond a grace period are Archived. Idempotency: only items that
/// actually transition are written back, so re-running the job changes nothing.
/// </summary>
public static class HygieneDefaults
{
    /// <summary>How long an Active item can go unused before decay applies.</summary>
    public const int DecayWindowDays = 30;

    /// <summary>Confidence lost per full decay window of inactivity.</summary>
    public const double DecayStep = 0.1;

    /// <summary>Ceiling on accumulated decay (a heavily-stale item never drops in one pass).</summary>
    public const double MaxDecay = 0.5;

    /// <summary>How long a Review item may stay untouched before it is Archived.</summary>
    public const int ReviewGraceDays = 7;
}
