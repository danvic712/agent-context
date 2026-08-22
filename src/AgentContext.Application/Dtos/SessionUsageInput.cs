namespace AgentContext.Application.Dtos;

/// <summary>
/// Usage reported by an agent for one completed conversation Session. The model
/// is the provider/model snapshot reported by the client; it is not a platform
/// inference route binding.
/// </summary>
public sealed record SessionUsageInput(
    string Model,
    int InputTokens,
    int CachedInputTokens,
    int OutputTokens);
