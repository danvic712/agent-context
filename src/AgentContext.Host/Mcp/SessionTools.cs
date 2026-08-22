using System.ComponentModel;
using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using ModelContextProtocol.Server;

namespace AgentContext.Host.Mcp;

/// <summary>
/// The v1 toolset's save_session tool (spec §6.1 / T2): reports a session to the
/// platform over MCP — domain tag, structured summary, optional remember, and
/// model and token fields which may be recorded in Usage; the legacy cost field is
/// accepted for contract compatibility but is not persisted. Errors are localized (T11) through the
/// shared translation service in the configured platform language.
/// </summary>
[McpServerToolType]
public sealed class SessionTools(
    ISaveSessionAppService sessions,
    ISettingsAppService settings,
    ITranslationService translations)
{
    [McpServerTool(Name = "save_session")]
    [Description("Records a session with the platform: a domain tag, a structured summary (task, conclusion, key snippets), optional remember of the full context, and model/token usage.")]
    public async Task<SaveSessionResult> SaveSession(
        [Description("Domain tag for the session, e.g. \"dev\" or \"home\". An unknown domain is registered automatically.")] string? domain,
        [Description("What the conversation set out to do.")] string task,
        [Description("Outcome / conclusion of the conversation.")] string conclusion,
        [Description("Key snippets worth keeping from the conversation.")] IReadOnlyList<string>? keySnippets = null,
        [Description("Store the full original context and mark the session important. Default: false.")] bool remember = false,
        [Description("Full original context; stored only when remember is true.")] string? fullContext = null,
        [Description("Name of the reporting agent instance.")] string? agentName = null,
        [Description("Model used, e.g. \"gpt-4o\".")] string? model = null,
        [Description("Input token count.")] int tokensIn = 0,
        [Description("Output token count.")] int tokensOut = 0,
        [Description("Legacy session cost in USD; accepted for compatibility but not persisted.")] decimal? cost = null,
        CancellationToken cancellationToken = default)
        => await McpErrorLocalizer.ExecuteAsync(settings, translations, () =>
            sessions.SaveAsync(
                new SaveSessionRequest(
                    domain, task, conclusion, keySnippets, remember, fullContext,
                    agentName, model, tokensIn, tokensOut, cost),
                cancellationToken), cancellationToken);
}
