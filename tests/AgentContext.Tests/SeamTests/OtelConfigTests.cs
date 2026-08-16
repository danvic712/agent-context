using AgentContext.Host.Observability;
using Microsoft.Extensions.Configuration;

namespace AgentContext.Tests.SeamTests;

/// <summary>
/// T13 (issue #14): the OTLP export gating shared by the Serilog sink and the
/// OTel SDK — on by default, with the standard OTEL_SDK_DISABLED / empty-endpoint
/// escape hatches. Pure configuration logic, no database.
/// </summary>
public class OtelConfigTests
{
    private static IConfiguration BuildConfig(params (string Key, string? Value)[] settings) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(settings
                .Where(s => s.Value is not null)
                .ToDictionary(s => s.Key, s => s.Value))
            .Build();

    [Fact]
    public void Export_is_enabled_by_default_with_the_standalone_endpoint()
    {
        var config = BuildConfig();

        Assert.True(OtelDefaults.IsOtlpExportEnabled(config));
        Assert.Equal("http://localhost:4317", OtelDefaults.GetOtlpEndpoint(config));
    }

    [Fact]
    public void Endpoint_comes_from_the_standard_environment_variable()
    {
        var config = BuildConfig(("OTEL_EXPORTER_OTLP_ENDPOINT", "http://collector.example:4318"));

        Assert.Equal("http://collector.example:4318", OtelDefaults.GetOtlpEndpoint(config));
        Assert.True(OtelDefaults.IsOtlpExportEnabled(config));
    }

    [Theory]
    [InlineData("true")]
    [InlineData("TRUE")]
    [InlineData("True")]
    public void OTEL_SDK_DISABLED_true_disables_export(string value)
    {
        var config = BuildConfig(("OTEL_SDK_DISABLED", value));

        Assert.False(OtelDefaults.IsOtlpExportEnabled(config));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Empty_endpoint_disables_export(string endpoint)
    {
        var config = BuildConfig(("OTEL_EXPORTER_OTLP_ENDPOINT", endpoint));

        Assert.False(OtelDefaults.IsOtlpExportEnabled(config));
    }

    [Fact]
    public void OTEL_SDK_DISABLED_false_keeps_export_enabled()
    {
        var config = BuildConfig(("OTEL_SDK_DISABLED", "false"));

        Assert.True(OtelDefaults.IsOtlpExportEnabled(config));
    }

    [Fact]
    public void Protocol_defaults_to_grpc_when_unset()
    {
        Assert.Equal("grpc", OtelDefaults.GetProtocolName(BuildConfig()));
    }

    [Fact]
    public void Protocol_honors_http_protobuf()
    {
        var config = BuildConfig(("OTEL_EXPORTER_OTLP_PROTOCOL", "http/protobuf"));

        Assert.Equal("http/protobuf", OtelDefaults.GetProtocolName(config));
    }

    [Fact]
    public void Service_name_defaults_to_agent_context_without_an_override()
    {
        Assert.Equal("agent-context", OtelDefaults.GetServiceName(BuildConfig()));
        Assert.Equal("agent-context", OtelDefaults.ResourceAttributes["service.name"]);
    }

    [Fact]
    public void Service_name_honors_the_standard_OTEL_SERVICE_NAME_override()
    {
        var config = BuildConfig(("OTEL_SERVICE_NAME", "portal"));

        Assert.Equal("portal", OtelDefaults.GetServiceName(config));
    }
}
