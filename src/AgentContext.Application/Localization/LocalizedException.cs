using System.Net;

namespace AgentContext.Application.Localization;

/// <summary>
/// A user-facing error carrying a stable <see cref="ErrorCode"/> (the translation
/// key under the <c>errors</c> namespace, e.g. <c>inference.baseUrlInvalid</c>) plus
/// positional arguments for interpolation. Call sites throw this instead of a
/// bare English <see cref="ArgumentException"/>; the REST surface renders the
/// <c>message</c> in the configured language (T11) and MCP tools localize through
/// the same <see cref="ILocalesAppService"/>.
/// </summary>
public sealed class LocalizedException : Exception
{
    /// <summary>HTTP status the REST surface maps this error to.</summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>Stable error code — never changes with the language.</summary>
    public string ErrorCode { get; }

    /// <summary>Positional arguments interpolated into the localized template.</summary>
    public object?[] Args { get; }

    /// <summary>Additional structured fields rendered by the REST adapter.</summary>
    public IReadOnlyDictionary<string, object?>? Details { get; }

    public LocalizedException(HttpStatusCode statusCode, string errorCode, params object?[] args)
        : base(errorCode)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
        Args = args;
    }

    public LocalizedException(
        HttpStatusCode statusCode,
        string errorCode,
        IReadOnlyDictionary<string, object?> details,
        params object?[] args)
        : this(statusCode, errorCode, args)
    {
        Details = details;
    }
}
