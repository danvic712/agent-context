namespace AgentContext.Application.Learning;

/// <summary>
/// Configured OpenAI-compatible LLM endpoint used by the Learning Engine
/// (ADR 0003): one base URL + key serves extraction and embedding in v1.
/// <c>EmbeddingModel</c> is optional — when omitted the extraction model is
/// reused — but the shape leaves room for separate models later.
/// Stored in the <c>settings</c> table (see <see cref="ISettingsAppService"/>),
/// not app configuration: the platform is setter-uppable at runtime.
/// </summary>
public sealed class LlmOptions
{
    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string? EmbeddingModel { get; set; }

    /// <summary>Embedding model used in v1: the dedicated one when set, else the extraction model.</summary>
    public string EffectiveEmbeddingModel => string.IsNullOrWhiteSpace(EmbeddingModel) ? Model : EmbeddingModel!;

    /// <summary>
    /// Returns the validation error codes (T11 — see <see cref="Localization.ErrorCodes"/>);
    /// empty means the options are valid. The surface renders the codes in the
    /// configured language from the shared JSON store.
    /// </summary>
    public static IReadOnlyList<string> Validate(LlmOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            errors.Add(Localization.ErrorCodes.Llm.BaseUrlRequired);
        }
        else if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
                 || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add(Localization.ErrorCodes.Llm.BaseUrlInvalid);
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            errors.Add(Localization.ErrorCodes.Llm.ApiKeyRequired);
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            errors.Add(Localization.ErrorCodes.Llm.ModelRequired);
        }

        return errors;
    }
}
