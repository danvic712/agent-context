using AgentContext.Application.Dtos;

namespace AgentContext.Application.Contracts;

/// <summary>
/// The maintained model pricing table (T7 / spec US28): per-model USD per-token
/// rates the platform uses to compute Usage cost, so Usage requires no extra
/// collection. Configurable at runtime.
/// </summary>
public interface IPricingAppService
{
    /// <summary>All pricing rows, ordered by model name.</summary>
    Task<IReadOnlyList<ModelPricingDto>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates or updates the row for the given model (upsert by model name).
    /// Throws <see cref="ArgumentException"/> for an empty model or a negative rate.
    /// </summary>
    Task<ModelPricingDto> SaveAsync(SaveModelPricingRequest request, CancellationToken cancellationToken = default);

    /// <summary>Deletes the pricing row for the given model; no-op when unknown.</summary>
    Task DeleteAsync(string model, CancellationToken cancellationToken = default);
}
