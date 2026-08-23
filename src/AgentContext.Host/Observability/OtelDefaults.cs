using Microsoft.Extensions.Configuration;

namespace AgentContext.Host.Observability;

/// <summary>
/// OpenTelemetry configuration shared by the Serilog OTLP sink and the OTel SDK
/// (traces + metrics). Export is opt-in through the standard OTLP environment
/// variables and can be disabled with <c>OTEL_SDK_DISABLED=true</c>.
/// </summary>
public static class OtelDefaults
{
    /// <summary>
    /// Default service.name when the host environment doesn't provide one.
    /// A host-provided <c>OTEL_SERVICE_NAME</c> wins (spec-conformant) — see
    /// <see cref="GetServiceName"/>. Keeps logs and traces attributed to the same
    /// name: the Serilog OTLP sink prefers OTEL_SERVICE_NAME too.
    /// </summary>
    public const string ServiceName = "agent-context";

    /// <summary>
    /// service.name for every signal: the standard <c>OTEL_SERVICE_NAME</c> when
    /// the host environment sets it, otherwise the fixed <see cref="ServiceName"/>
    /// default.
    /// </summary>
    public static string GetServiceName(IConfiguration configuration)
    {
        var fromEnvironment = configuration["OTEL_SERVICE_NAME"];
        return string.IsNullOrWhiteSpace(fromEnvironment) ? ServiceName : fromEnvironment;
    }

    /// <summary>
    /// Custom ActivitySource emitted by the Learning Engine pipeline
    /// (<see cref="AgentContext.Application.Learning.LearningPipelineTelemetry"/>),
    /// registered with the trace provider so pipeline runs surface as traces.
    /// </summary>
    public const string LearningPipelineActivitySource = "AgentContext.LearningPipeline";

    /// <summary>
    /// The standard OTLP endpoint override (spec-conformant env var). A
    /// null/whitespace value disables export.
    /// </summary>
    public static string? GetOtlpEndpoint(IConfiguration configuration) =>
        configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

    /// <summary>
    /// <c>OTEL_SDK_DISABLED=true</c> or an empty endpoint turns the whole
    /// OpenTelemetry stack (Serilog sink + traces + metrics) off while the app
    /// itself behaves identically.
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
