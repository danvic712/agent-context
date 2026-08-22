namespace AgentContext.Application.Dtos;

/// <summary>Canonical token counts reported by a provider for one platform call.</summary>
public sealed record LlmUsage(
    int InputTokens,
    int CachedInputTokens,
    int OutputTokens);
