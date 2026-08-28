using AgentContext.Application.Contracts;
using AgentContext.Application.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace AgentContext.Host;

/// <summary>
/// T11 error surface: every <see cref="LocalizedException"/> thrown by an
/// application service is rendered as <c>{ errorCode, message }</c> — the stable
/// code plus the message translated into the configured platform language
/// (fallback en-US, then the raw key). Controllers don't catch these themselves;
/// this filter is the single error seam on the REST surface.
/// </summary>
public sealed class LocalizedExceptionFilter(
    ILocalesAppService locales,
    ISettingsAppService settings) : IAsyncExceptionFilter
{
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.Exception is not LocalizedException ex)
        {
            return;
        }

        var locale = await settings.GetLanguageAsync(context.HttpContext.RequestAborted);

        var body = new Dictionary<string, object?>
        {
            ["errorCode"] = ex.ErrorCode,
            ["message"] = locales.GetError(ex.ErrorCode, locale, ex.Args),
        };
        if (ex.Details is not null)
        {
            foreach (var detail in ex.Details)
            {
                body[detail.Key] = detail.Value;
            }
        }

        context.Result = new ObjectResult(body)
        {
            StatusCode = (int)ex.StatusCode,
        };
        context.ExceptionHandled = true;
    }
}
