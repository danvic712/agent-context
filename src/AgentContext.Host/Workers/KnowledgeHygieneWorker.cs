using AgentContext.Application.Contracts;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgentContext.Host.Workers;

/// <summary>
/// Periodic scheduler for Knowledge hygiene (T8 / ADR 0005: "Cleanup runs on a
/// PeriodicTimer"). Only a scheduler — the decay/review/archive behaviour lives
/// in <see cref="IKnowledgeHygieneAppService"/>, so tests drive the same seam
/// directly. Runs less often than the session worker: hygiene is cheap to skip.
/// </summary>
public sealed class KnowledgeHygieneWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<KnowledgeHygieneWorker> logger,
    TimeSpan? interval = null) : BackgroundService
{
    private readonly TimeSpan _interval = interval ?? TimeSpan.FromMinutes(5);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(_interval);

        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var hygiene = scope.ServiceProvider.GetRequiredService<IKnowledgeHygieneAppService>();
                var result = await hygiene.RunOnceAsync(stoppingToken);

                if (result.Decayed > 0 || result.MovedToReview > 0 || result.Archived > 0)
                {
                    logger.LogInformation(
                        "Hygiene pass: {Decayed} decayed, {MovedToReview} moved to review, {Archived} archived.",
                        result.Decayed, result.MovedToReview, result.Archived);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Hygiene tick failed; will retry on the next tick.");
            }
        }
    }
}
