namespace AgentContext.Application.Settings;

/// <summary>Keys of the platform settings stored in the <c>settings</c> table.</summary>
public static class SettingKeys
{
    public const string LlmBaseUrl = "llm.baseUrl";
    public const string LlmApiKey = "llm.apiKey";
    public const string LlmModel = "llm.model";
    public const string LlmEmbeddingModel = "llm.embeddingModel";

    /// <summary>Platform language (BCP-47 locale, T11): "en-US" | "zh-CN", missing → en-US.</summary>
    public const string Language = "language";

    /// <summary>Platform theme (T12): "light" | "dark" | "system", missing → system.</summary>
    public const string Theme = "theme";
}
