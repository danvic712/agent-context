using System.Net;
using AgentContext.Tests.Testcontainers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;

namespace AgentContext.Tests.AdapterTests;

/// <summary>
/// T13 (issue #14) — the web host boots with the OpenTelemetry stack on by
/// default, and the standard escape hatches (OTEL_SDK_DISABLED=true, empty OTLP
/// endpoint) turn it off without changing app behaviour. Provider presence is
/// asserted through the host's DI container; the OTLP exporter targets
/// localhost:4317 (unreachable in tests), which only fails exports in the
/// background — it never affects request handling.
/// </summary>
public sealed class OtelObservabilityTests : PostgresTestBase
{
    private WebApplicationFactory<Program> CreateFactory(Action<IWebHostBuilder>? configure = null)
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseSetting("ConnectionStrings:Default", Fixture.ConnectionString);
                configure?.Invoke(builder);
            });
        return factory;
    }

    [Fact]
    public async Task Web_host_boots_with_otel_enabled_by_default()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        // The app behaves normally with the OTel stack registered.
        var response = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // Default boot registers the trace + metric providers (export on by default).
        Assert.NotNull(factory.Services.GetService(typeof(TracerProvider)));
        Assert.NotNull(factory.Services.GetService(typeof(MeterProvider)));
    }

    [Fact]
    public async Task OTEL_SDK_DISABLED_true_skips_the_otel_stack()
    {
        using var factory = CreateFactory(builder => builder.UseSetting("OTEL_SDK_DISABLED", "true"));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Null(factory.Services.GetService(typeof(TracerProvider)));
        Assert.Null(factory.Services.GetService(typeof(MeterProvider)));
    }

    [Fact]
    public async Task Empty_otlp_endpoint_skips_the_otel_stack()
    {
        using var factory = CreateFactory(builder => builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", ""));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Null(factory.Services.GetService(typeof(TracerProvider)));
        Assert.Null(factory.Services.GetService(typeof(MeterProvider)));
    }

    [Fact]
    public async Task Custom_otlp_endpoint_is_honored()
    {
        using var factory = CreateFactory(builder =>
            builder.UseSetting("OTEL_EXPORTER_OTLP_ENDPOINT", "http://127.0.0.1:9999"));
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // A custom endpoint keeps the stack on — providers registered.
        Assert.NotNull(factory.Services.GetService(typeof(TracerProvider)));
        Assert.NotNull(factory.Services.GetService(typeof(MeterProvider)));
    }
}
