namespace AgentContext.Domain.Entities;

/// <summary>
/// A platform setting stored in the database (spec: "settings (LLM endpoint)").
/// Key/value rows; the Learning Engine's LLM endpoint configuration lives here
/// (ADR 0003) so it is setter-uppable at runtime instead of via app config.
/// </summary>
public sealed class AppSetting
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
