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

// Dual-mode entrypoint (ADR 0006): `--mcp-stdio` runs the MCP server over stdio for
// Craft Agents local sources; anything else (default / `--web`) runs the ASP.NET
// Core host serving the REST API + React UI. Both share one DI graph via
// AddApplicationServices.
if (args.Contains("--mcp-stdio"))
{
    return await McpStdioHost.RunAsync(args);
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

// Serve the React UI (built into wwwroot by the SPA target; see web/ and csproj).
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

await app.RunAsync();
return 0;

/// <summary>Exposed so WebApplicationFactory integration tests can boot the web mode.</summary>
public partial class Program { }
