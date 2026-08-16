namespace AgentContext.Application.Dtos;

/// <summary>
/// What a single hygiene run did (T8 / spec US20): items decayed (Confidence
/// reduced in place), items moved to Review, and Review items Archived.
/// </summary>
public sealed record HygieneResult(
    int Decayed,
    int MovedToReview,
    int Archived);
