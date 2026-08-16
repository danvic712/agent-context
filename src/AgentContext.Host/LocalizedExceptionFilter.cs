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
    ITranslationService translations,
    ISettingsAppService settings) : IAsyncExceptionFilter
{
    public async Task OnExceptionAsync(ExceptionContext context)
    {
        if (context.Exception is not LocalizedException ex)
        {
            return;
        }

        var locale = await settings.GetLanguageAsync(context.HttpContext.RequestAborted);

        context.Result = new ObjectResult(new
        {
            errorCode = ex.ErrorCode,
            message = translations.GetError(ex.ErrorCode, locale, ex.Args),
        })
        {
            StatusCode = (int)ex.StatusCode,
        };
        context.ExceptionHandled = true;
    }
}
