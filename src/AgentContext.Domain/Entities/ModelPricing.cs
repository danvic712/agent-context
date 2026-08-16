namespace AgentContext.Domain.Entities;

/// <summary>
/// A maintained model pricing row (spec US28 / §6.2): the platform computes
/// cost = tokensIn × inputRate + tokensOut × outputRate against this table,
/// so Usage requires no extra collection. One row per model.
/// </summary>
public sealed class ModelPricing
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>Model identifier, e.g. "gpt-4o" (matches Usage.Model).</summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>USD per input token.</summary>
    public decimal InputCostPerToken { get; set; }

    /// <summary>USD per output token.</summary>
    public decimal OutputCostPerToken { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}
