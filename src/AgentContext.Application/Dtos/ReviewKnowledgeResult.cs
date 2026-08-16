namespace AgentContext.Application.Dtos;

/// <summary>
/// The review list plus the Confidence threshold that defines it — the UI shows
/// the threshold from the backend instead of hardcoding it.
/// </summary>
public sealed record ReviewKnowledgeResult(
    double Threshold,
    IReadOnlyList<KnowledgeListItem> Items);
