using System.Diagnostics;

namespace AgentContext.Application.Learning;

/// <summary>
/// ActivitySource for the Learning Engine pipeline so pipeline runs surface as
/// traces in an OpenTelemetry-compatible backend. Uses only
/// System.Diagnostics (BCL, no OTel dependency) — the host's trace provider
/// subscribes via the shared ActivitySource name.
/// </summary>
public static class LearningPipelineTelemetry
{
    public static readonly ActivitySource Source = new("AgentContext.LearningPipeline");
}
