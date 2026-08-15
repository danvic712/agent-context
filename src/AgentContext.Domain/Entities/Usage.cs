namespace AgentContext.Domain.Entities;

/// <summary>
/// A record of token consumption and cost attached to a Session, broken down by
/// model (CONTEXT.md). Cost = tokens × maintained pricing table (spec §6.2).
/// </summary>
public sealed class Usage
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid SessionId { get; set; }

    public string Model { get; set; } = string.Empty;

    public int TokensIn { get; set; }
    public int TokensOut { get; set; }

    public decimal Cost { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public Session Session { get; set; } = null!;
}
