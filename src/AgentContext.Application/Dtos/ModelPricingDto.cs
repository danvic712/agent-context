namespace AgentContext.Application.Dtos;

/// <summary>One maintained pricing row (spec US28): per-token USD rates for a model.</summary>
public sealed record ModelPricingDto(
    Guid Id,
    string Model,
    decimal InputCostPerToken,
    decimal OutputCostPerToken,
    DateTimeOffset UpdatedAtUtc);
