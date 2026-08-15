using AgentContext.Application;
using AgentContext.Infrastructure;
using AgentContext.Host.Mcp;
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

builder.Services.AddControllers();
builder.Services.AddApplicationServices(builder.Configuration);

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

app.Run();
return 0;

/// <summary>Exposed so WebApplicationFactory integration tests can boot the web mode.</summary>
public partial class Program { }
