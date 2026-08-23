using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace AgentContext.Host.Observability;

/// <summary>
/// The three-signal OpenTelemetry stack. Traces and metrics go through the OTel
/// SDK (ASP.NET Core instrumentation + the Learning Engine ActivitySource + the
/// runtime's default meters); logs are written by Serilog's OTLP sink in
/// Program.cs. Both share the endpoint/protocol/resource from
/// <see cref="OtelDefaults"/>.
/// </summary>
public static class OtelServiceExtensions
{
    public static IServiceCollection AddOtelObservability(this IServiceCollection services, IConfiguration configuration)
    {
        // OTEL_SDK_DISABLED=true or an empty endpoint: no SDK at all, app unchanged.
        if (!OtelDefaults.IsOtlpExportEnabled(configuration))
        {
            return services;
        }

        var endpoint = new Uri(OtelDefaults.GetOtlpEndpoint(configuration)!);
        var protocol = OtelDefaults.GetProtocolName(configuration);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource
                // Resource attributes populated from the host environment: honors the
                // standard OTEL_SERVICE_NAME / OTEL_RESOURCE_ATTRIBUTES variables.
                .AddEnvironmentVariableDetector()
                // service.name defaults to agent-context, but a host-provided
                // OTEL_SERVICE_NAME wins (spec-conformant). This keeps logs and traces
                // attributed to the same name: the Serilog OTLP sink also honors
                // OTEL_SERVICE_NAME.
                .AddAttributes(new Dictionary<string, object>
                {
                    ["service.name"] = OtelDefaults.GetServiceName(configuration),
                }))
            .WithTracing(tracing => tracing
                .AddSource(OtelDefaults.LearningPipelineActivitySource)
                .AddAspNetCoreInstrumentation()
                .AddOtlpExporter(options => ConfigureOtlp(options, endpoint, protocol)))
            .WithMetrics(metrics => metrics
                // ASP.NET Core built-in meters (http.server.request.duration etc.) plus
                // the runtime's outbound HTTP client meter (System.Net.Http).
                .AddAspNetCoreInstrumentation()
                .AddMeter("System.Net.Http")
                .AddOtlpExporter(options => ConfigureOtlp(options, endpoint, protocol)));

        return services;
    }

    private static void ConfigureOtlp(OtlpExporterOptions options, Uri endpoint, string protocol)
    {
        options.Endpoint = endpoint;
        options.Protocol = protocol == "http/protobuf" ? OtlpExportProtocol.HttpProtobuf : OtlpExportProtocol.Grpc;
    }
}
