using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AgentContext.Application.Pricing;

/// <inheritdoc cref="IPricingAppService"/>
/// <summary>
/// The maintained model pricing table (T7 / spec US28): per-model USD per-token
/// rates, upserted by model name. The Learning/analytics rollup reads these rows
/// to compute cost, so Usage needs no extra collection.
/// </summary>
public sealed class PricingAppService(AgentContextDbContext db) : IPricingAppService
{
    public async Task<IReadOnlyList<ModelPricingDto>> ListAsync(CancellationToken cancellationToken = default)
        => await db.ModelPricings.AsNoTracking()
            .OrderBy(p => p.Model)
            .Select(p => new ModelPricingDto(p.Id, p.Model, p.InputCostPerToken, p.OutputCostPerToken, p.UpdatedAtUtc))
            .ToListAsync(cancellationToken);

    public async Task<ModelPricingDto> SaveAsync(SaveModelPricingRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var model = request.Model.Trim();
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ArgumentException("Model name is required.", nameof(request));
        }

        if (request.InputCostPerToken < 0 || request.OutputCostPerToken < 0)
        {
            throw new ArgumentException("Per-token costs cannot be negative.", nameof(request));
        }

        var existing = await db.ModelPricings.FirstOrDefaultAsync(p => p.Model == model, cancellationToken);
        if (existing is null)
        {
            existing = new ModelPricing { Model = model };
            db.ModelPricings.Add(existing);
        }

        existing.InputCostPerToken = request.InputCostPerToken;
        existing.OutputCostPerToken = request.OutputCostPerToken;
        existing.UpdatedAtUtc = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return new ModelPricingDto(existing.Id, existing.Model, existing.InputCostPerToken, existing.OutputCostPerToken, existing.UpdatedAtUtc);
    }

    public async Task DeleteAsync(string model, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            return;
        }

        await db.ModelPricings.Where(p => p.Model == model.Trim()).ExecuteDeleteAsync(cancellationToken);
    }
}
