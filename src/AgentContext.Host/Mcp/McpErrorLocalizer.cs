using AgentContext.Application.Contracts;
using AgentContext.Application.Localization;

namespace AgentContext.Host.Mcp;

/// <summary>
/// T11 error surface for MCP tools: a <see cref="LocalizedException"/> thrown by
/// an application service is rendered into the configured platform language and
/// rethrown so the MCP framework returns an <c>isError</c> tool result with the
/// localized message as its text — the same translation service the REST surface
/// uses (ADR 0008).
/// </summary>
internal static class McpErrorLocalizer
{
    public static async Task<T> ExecuteAsync<T>(
        ISettingsAppService settings,
        ITranslationService translations,
        Func<Task<T>> action,
        CancellationToken cancellationToken)
    {
        try
        {
            return await action();
        }
        catch (LocalizedException ex)
        {
            var locale = await settings.GetLanguageAsync(cancellationToken);
            throw new InvalidOperationException(translations.GetError(ex.ErrorCode, locale, ex.Args));
        }
    }
}
