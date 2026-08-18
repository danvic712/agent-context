using System.Reflection;
using AgentContext.Application;
using AgentContext.Host;
using AgentContext.Host.AppHost;
using AgentContext.Host.DashboardProxy;
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
// together with the UI. Postgres is orchestrated by Aspire only when no
// ConnectionStrings__Default is present (bare local `dotnet run`); the
// container image and compose rely on an external PostgreSQL (issue #15).
//
// The only conditionals are the internal HOST_MODE=portal role marker injected
// into the portal child process by the orchestrator (it makes that process
// serve the portal instead of re-orchestrating — nested containers are
// impossible) and the entry-assembly check below. Neither is a user-facing mode
// or argument: the AppHost orchestration only belongs to the standalone binary.
// WebApplicationFactory (Mvc.Testing) launches this entrypoint from the test
// assembly, which must run the portal host directly — not a nested
// DistributedApplication (no DCP, and orchestrating containers from a unit
// test host makes no sense).
if (Environment.GetEnvironmentVariable("HOST_MODE") != "portal" &&
    Assembly.GetEntryAssembly() == typeof(Program).Assembly)
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

// Single-port model (issue #15): when AppHost orchestration injected
// DASHBOARD_ORIGIN, reverse-proxy /monitor/* to the in-process Aspire
// dashboard (YARP handles the Blazor websocket circuit + long-polling).
builder.Services.AddDashboardProxy(builder.Configuration["DASHBOARD_ORIGIN"]);

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

// Single-port model (issue #15): /monitor/* -> in-process Aspire dashboard.
// UseWebSockets lets the proxy forward the Blazor interactive circuit
// (websocket upgrade to /monitor/_blazor). Registered before the SPA
// fallback so the proxy route wins.
app.UseWebSockets();
app.MapReverseProxy();

// Dashboard page redirect (issue #15): the dashboard's nav links are hard-coded
// root paths (/consolelogs, /metrics, ...). Those root-path page requests are
// redirected to their /monitor-prefixed equivalent so every dashboard route
// stays under /monitor while the portal keeps owning "/".
//
// A 302 (rather than a 200 + blazor-enhanced-nav-redirect-location header) is
// used because Blazor's enhanced navigation follows the redirect and keeps the
// final URL via history.pushState without a full-page reload — so component
// navigations (e.g. the metrics page auto-selecting a resource) don't flash.
// Link navigations are already rewritten to the prefix by navfix.js and never
// reach this middleware.
app.Use(async (context, next) =>
{
    if (HttpMethods.IsGet(context.Request.Method) &&
        DashboardProxySetup.TryGetDashboardPrefixPath(context.Request.Path, out var target))
    {
        context.Response.Redirect(target + context.Request.QueryString);
        return;
    }

    await next();
});

// Serve the React UI (built into wwwroot by the SPA target; see web/ and csproj).
app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

await app.RunAsync();
return 0;

/// <summary>Exposed so WebApplicationFactory integration tests can boot the web mode.</summary>
public partial class Program { }
