using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentContext.Host.Workers;

/// <summary>
/// Periodic scheduler for the Postgres-as-queue (ADR 0005): polls for an
/// eligible pending Session and runs the Learning Engine pipeline on it.
/// Only a scheduler — the behaviour lives in <see cref="ILearningPipelineAppService"/>
/// (T3, issue #4), so the same seam drives tests and the worker (AC5).
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
                var pipeline = scope.ServiceProvider.GetRequiredService<ILearningPipelineAppService>();
                var result = await pipeline.ProcessNextAsync(stoppingToken);

                if (result.Outcome is not (PipelineOutcome.Idle or PipelineOutcome.NotClaimed))
                {
                    logger.LogInformation(
                        "Pipeline {Outcome} for session {SessionId}: {Created} Knowledge created, " +
                        "{Corroborated} corroborated.",
                        result.Outcome, result.SessionId, result.KnowledgeCreated, result.KnowledgeCorroborated);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Learning Engine tick failed; will retry on the next tick.");
            }
        }
    }
}
