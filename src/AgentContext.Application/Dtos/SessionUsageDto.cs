namespace AgentContext.Application.Dtos;

/// <summary>One Usage row exposed on a session detail.</summary>
public sealed record SessionUsageDto(string Model, int TokensIn, int TokensOut, decimal Cost);
