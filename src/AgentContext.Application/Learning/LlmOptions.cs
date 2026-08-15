using Microsoft.Extensions.Options;

namespace AgentContext.Application.Learning;

/// <summary>
/// Configured OpenAI-compatible LLM endpoint used by the Learning Engine
/// (ADR 0003): one base URL + key serves extraction and embedding in v1.
/// <c>EmbeddingModel</c> is optional — when omitted the extraction model is
/// reused — but the shape leaves room for separate models later.
/// Binds to the <c>Llm</c> configuration section (env-overridable, e.g.
/// <c>Llm__BaseUrl</c>); the compose app env is the settings surface.
/// </summary>
public sealed class LlmOptions
{
    public const string SectionName = "Llm";

    public string BaseUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string? EmbeddingModel { get; set; }

    /// <summary>Embedding model used in v1: the dedicated one when set, else the extraction model.</summary>
    public string EffectiveEmbeddingModel => string.IsNullOrWhiteSpace(EmbeddingModel) ? Model : EmbeddingModel!;

    /// <summary>Returns the validation error messages; empty means the options are valid.</summary>
    public static IReadOnlyList<string> Validate(LlmOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(options.BaseUrl))
        {
            errors.Add("Llm:BaseUrl is required (e.g. https://api.openai.com/v1 or http://localhost:11434/v1).");
        }
        else if (!Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out var uri)
                 || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            errors.Add("Llm:BaseUrl must be an absolute http(s) URL.");
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            errors.Add("Llm:ApiKey is required.");
        }

        if (string.IsNullOrWhiteSpace(options.Model))
        {
            errors.Add("Llm:Model is required.");
        }

        return errors;
    }
}

/// <summary>
/// Runs on every <c>IOptions&lt;LlmOptions&gt;</c> resolve, so an invalid or
/// missing LLM configuration surfaces as <see cref="OptionsValidationException"/>
/// where the Learning Engine actually uses it (the worker tick) — the platform
/// itself stays up for setup/settings without blocking startup.
/// </summary>
public sealed class LlmOptionsValidator : IValidateOptions<LlmOptions>
{
    public ValidateOptionsResult Validate(string? name, LlmOptions options)
    {
        var errors = LlmOptions.Validate(options);
        return errors.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(errors);
    }
}
