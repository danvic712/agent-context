using AgentContext.Application;
using AgentContext.Host;
using AgentContext.Host.AppHost;
using AgentContext.Host.Mcp;
using AgentContext.Host.Observability;
using AgentContext.Host.Workers;
using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Serilog;
using Serilog.Sinks.OpenTelemetry;
// Aspire.Hosting.AppHost injects a global `using Aspire.Hosting` which also declares an
// OtlpProtocol enum — alias the Serilog one so the sink's protocol stays unambiguous.
using OtlpProtocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol;

// Single-binary entrypoint (ADR 0006). There is exactly one behaviour and no
// startup flags: running the binary starts the complete environment — postgres
// + portal (UI + REST API + MCP /mcp) + Aspire dashboard — orchestrated as one
// DistributedApplication. The dashboard is useless alone, so it always comes up
// together with the UI.
//
// The only conditional is the internal HOST_MODE=portal role marker injected
// into the portal child process by the orchestrator (and set on the container
// image): it makes that process serve the portal instead of re-orchestrating
// (nested containers are impossible). It is not a user-facing mode or argument.
if (Environment.GetEnvironmentVariable("HOST_MODE") != "portal")
{
    return await AppHostRunner.RunAsync(args);
}

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration);

    // T13 (issue #14): dual-write structured logs to the Aspire dashboard over OTLP.
    // Skipped by the same escape hatches as the OTel SDK (OTEL_SDK_DISABLED / empty endpoint).
    if (OtelDefaults.IsOtlpExportEnabled(context.Configuration))
    {
        var otelConfig = context.Configuration;
        configuration.WriteTo.OpenTelemetry(options =>
        {
            options.Endpoint = OtelDefaults.GetOtlpEndpoint(otelConfig)!;
            options.Protocol = OtelDefaults.GetProtocolName(otelConfig) == "http/protobuf"
                ? OtlpProtocol.HttpProtobuf
                : OtlpProtocol.Grpc;
            // Correlate log records with their ASP.NET Core / pipeline activity.
            options.IncludedData = IncludedData.TraceIdField | IncludedData.SpanIdField;
            options.ResourceAttributes = new Dictionary<string, object>(OtelDefaults.ResourceAttributes);
        });
    }
});

builder.Services.AddControllers(options =>
        // T11: LocalizedException → { errorCode, message } in the configured language.
        options.Filters.Add<LocalizedExceptionFilter>())
    // Enums travel as strings on the REST surface (matches the DB's string columns).
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddApplicationServices(builder.Configuration);

// T14: the v1 MCP toolset over Streamable HTTP — Craft Agents connects to
// /mcp by URL (https://agent-context.orb.local/mcp). Stateless per the SDK's
// recommendation: no session affinity, no Mcp-Session-Id dependency, scales
// horizontally. Same DI graph (DbContext, settings, LLM client) as the REST
// surface.
builder.Services.AddAgentContextMcp()
    .WithHttpTransport(options => options.Stateless = true);

// T13 (issue #14): OpenTelemetry traces + metrics, on by default (OTLP export to
// the Aspire dashboard). Logs are wired through the Serilog sink above.
builder.Services.AddOtelObservability(builder.Configuration);

// Postgres-as-queue scheduler (ADR 0005): marks pending Sessions processed.
builder.Services.AddHostedService<SessionProcessingWorker>();

// Knowledge hygiene (T8): temporal decay → review → archive on a PeriodicTimer.
builder.Services.AddHostedService<KnowledgeHygieneWorker>();

var app = builder.Build();

// No manual steps: ensure the database exists, then apply EF Core migrations at
// startup against Postgres(pgvector). Exists/Create is migration-compatible
// (unlike EnsureCreated, which would bypass the migration history).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AgentContextDbContext>();
    var creator = db.GetService<IRelationalDatabaseCreator>();
    if (!await creator.ExistsAsync())
    {
        await creator.CreateAsync();
    }

    await db.Database.MigrateAsync();
}

app.UseSerilogRequestLogging();
app.MapControllers();

// T14: Streamable HTTP MCP endpoint — the only MCP surface, one URL for
// remote clients. Unauthenticated in MVP.
app.MapMcp("/mcp");

// Serve the React UI (built into wwwroot by the SPA target; see web/ and csproj).
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

await app.RunAsync();
return 0;

/// <summary>Exposed so WebApplicationFactory integration tests can boot the web mode.</summary>
public partial class Program { }
