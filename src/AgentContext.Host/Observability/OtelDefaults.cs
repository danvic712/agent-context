using Microsoft.Extensions.Configuration;

namespace AgentContext.Host.Observability;

/// <summary>
/// T13 (issue #14): OpenTelemetry configuration shared by the Serilog OTLP sink
/// and the OTel SDK (traces + metrics). All three signals are on by default and
/// export to the Aspire dashboard; the standard escape hatches
/// (<c>OTEL_SDK_DISABLED=true</c>, or an empty <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>)
/// disable export without changing app behaviour.
/// </summary>
public static class OtelDefaults
{
    /// <summary>service.name for every signal, fixed regardless of the host environment.</summary>
    public const string ServiceName = "agent-context";

    /// <summary>
    /// Standalone default OTLP/gRPC endpoint. Matches the compose host mapping of
    /// the aspire-dashboard service (host 4317 → container 18889); docker-compose
    /// overrides the endpoint to the in-network dashboard address.
    /// </summary>
    public const string DefaultOtlpEndpoint = "http://localhost:4317";

    /// <summary>
    /// Custom ActivitySource emitted by the Learning Engine pipeline
    /// (<see cref="AgentContext.Application.Learning.LearningPipelineTelemetry"/>),
    /// registered with the trace provider so pipeline runs surface as traces.
    /// </summary>
    public const string LearningPipelineActivitySource = "AgentContext.LearningPipeline";

    /// <summary>
    /// The standard OTLP endpoint override (spec-conformant env var). Returns the
    /// standalone default when unset; a null/whitespace value disables export.
    /// </summary>
    public static string? GetOtlpEndpoint(IConfiguration configuration) =>
        configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? DefaultOtlpEndpoint;

    /// <summary>
    /// Escape hatch: <c>OTEL_SDK_DISABLED=true</c> or an empty endpoint turns the
    /// whole OpenTelemetry stack (Serilog sink + traces + metrics) off while the
    /// app itself behaves identically.
    /// </summary>
    public static bool IsOtlpExportEnabled(IConfiguration configuration)
    {
        if (string.Equals(configuration["OTEL_SDK_DISABLED"], "true", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(GetOtlpEndpoint(configuration));
    }

    /// <summary>
    /// OTLP transport protocol, honoring <c>OTEL_EXPORTER_OTLP_PROTOCOL</c>
    /// ("grpc" | "http/protobuf"); defaults to gRPC to match the :4317 convention.
    /// </summary>
    public static string GetProtocolName(IConfiguration configuration) =>
        string.Equals(configuration["OTEL_EXPORTER_OTLP_PROTOCOL"], "http/protobuf", StringComparison.OrdinalIgnoreCase)
            ? "http/protobuf"
            : "grpc";

    /// <summary>Resource attributes shared by the Serilog sink and the OTel SDK.</summary>
    public static IReadOnlyDictionary<string, object> ResourceAttributes { get; } =
        new Dictionary<string, object> { ["service.name"] = ServiceName };
}
