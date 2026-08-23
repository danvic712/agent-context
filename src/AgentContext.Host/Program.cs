using AgentContext.Application;
using AgentContext.Host;
using AgentContext.Host.Mcp;
using AgentContext.Host.Observability;
using AgentContext.Host.Workers;
using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Serilog;
using Serilog.Sinks.OpenTelemetry;
using OtlpProtocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol;

// The single public entrypoint serves the REST API, React UI, and Streamable HTTP
// MCP surface. PostgreSQL is supplied through the configured connection string.
var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration);

    // Export structured logs to the configured OTLP collector when enabled.
    // OTEL_SDK_DISABLED or an empty endpoint leaves the local console sink intact.
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

// OpenTelemetry traces + metrics are exported when an OTLP endpoint is configured.
// Logs are wired through the Serilog sink above.
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
app.UseRouting();
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
