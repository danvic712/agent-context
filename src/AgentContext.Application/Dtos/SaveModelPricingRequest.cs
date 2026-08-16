namespace AgentContext.Application.Dtos;

/// <summary>Input for creating or updating a model's pricing row (upsert by Model).</summary>
public sealed record SaveModelPricingRequest(
    string Model,
    decimal InputCostPerToken,
    decimal OutputCostPerToken);
