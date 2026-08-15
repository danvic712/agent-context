using AgentContext.Application.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentContext.Host.Workers;

/// <summary>
/// Periodic scheduler for the Postgres-as-queue (ADR 0005): polls for pending
/// Sessions and marks them processed. No extraction yet (T3). Only a scheduler —
/// the behaviour lives in <see cref="ISessionProcessingAppService"/>.
/// </summary>
public sealed class SessionProcessingWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<SessionProcessingWorker> logger,
    TimeSpan? pollingInterval = null) : BackgroundService
{
    private readonly TimeSpan _interval = pollingInterval ?? TimeSpan.FromSeconds(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<ISessionProcessingAppService>();
                var processed = await processor.MarkProcessedAsync(stoppingToken);
                if (processed > 0)
                {
                    logger.LogInformation("Marked {Count} pending session(s) processed.", processed);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Session processing tick failed; will retry on the next tick.");
            }
        }
    }
}
