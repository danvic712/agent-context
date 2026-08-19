namespace AgentContext.Domain.Entities;

/// <summary>
/// A platform preference stored in the database.
/// Key/value rows; language and theme live here. Inference connections use
/// dedicated tables so provider routes can be configured independently.
/// </summary>
public sealed class AppSetting
{
    public string Key { get; set; } = string.Empty;

    public string Value { get; set; } = string.Empty;
}
