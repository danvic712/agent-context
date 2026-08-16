using AgentContext.Application;
using AgentContext.Host.Mcp;
using AgentContext.Host.Workers;
using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Serilog;

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
    configuration.ReadFrom.Configuration(context.Configuration));

builder.Services.AddControllers()
    // Enums travel as strings on the REST surface (matches the DB's string columns).
    .AddJsonOptions(options =>
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddApplicationServices(builder.Configuration);

// Postgres-as-queue scheduler (ADR 0005): marks pending Sessions processed.
builder.Services.AddHostedService<SessionProcessingWorker>();

var app = builder.Build();

// No manual steps: apply EF Core migrations at startup against Postgres(pgvector).
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AgentContextDbContext>();
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
