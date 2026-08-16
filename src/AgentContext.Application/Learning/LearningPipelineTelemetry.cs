using System.Diagnostics;

namespace AgentContext.Application.Learning;

/// <summary>
/// T13 (issue #14): ActivitySource for the Learning Engine pipeline so pipeline
/// runs surface as traces in the OpenTelemetry dashboard. Uses only
/// System.Diagnostics (BCL, no OTel dependency) — the host's trace provider
/// subscribes via OtelDefaults.LearningPipelineActivitySource.
/// </summary>
public static class LearningPipelineTelemetry
{
    public static readonly ActivitySource Source = new("AgentContext.LearningPipeline");
}
