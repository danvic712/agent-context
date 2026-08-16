using System.Reflection;
using Aspire.Hosting;
using Aspire.Hosting.Postgres;

namespace AgentContext.Host.AppHost;

/// <summary>
/// T13 follow-up: the <c>--apphost</c> entrypoint. Runs this same binary as an
/// Aspire DistributedApplication so the dashboard gains the Resources / service
/// view that the standalone (compose) dashboard lacks — the resource service is
/// AppHost-only. Models the platform as two resources: postgres (pgvector
/// container) and the portal itself (a child process of this binary with
/// <c>--web</c>). The dashboard is hosted in-process by Aspire; the portal opts
/// into OTLP export via <c>WithOtlpExporter()</c> (AddExecutable resources aren't
/// covered by the automatic env injection), which the portal's T13 stack
/// (<see cref="Observability.OtelDefaults"/>, reads <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>)
/// picks up as-is — logs/traces/metrics flow to the same dashboard that shows
/// the Resources view.
/// </summary>
public static class AppHostRunner
{
    public static async Task<int> RunAsync(string[] args)
    {
        // Filter out our own flag so the DistributedApplication argument parser
        // only sees configuration it understands.
        var appArgs = args.Where(a => a != "--apphost").ToArray();

        // Local development only: the in-process dashboard serves plain HTTP on
        // localhost (like the compose standalone container); a launch-profile
        // equivalent for the --apphost flag path.
        Environment.SetEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true");

        var builder = DistributedApplication.CreateBuilder(appArgs);

        var repoRoot = FindRepoRoot();
        var baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

        // Fixed password (not Aspire's auto-generated one): a named data volume is
        // initialized with the password seen on first start, so a regenerated
        // password on the next run would fail authentication against the old volume.
        var pgPassword = builder.AddParameter("postgres-password", "agent_context");

        var postgres = builder.AddPostgres("postgres", password: pgPassword)
            .WithImage("pgvector/pgvector")
            .WithImageTag("pg17")
            .WithDataVolume("agentcontext-pgdata");

        builder.AddExecutable("portal", "dotnet", baseDir, "AgentContext.Host.dll", "--web")
            // Non-proxied so the portal binds 8080 directly (same surface as compose);
            // DCP's proxied endpoints don't inject ASPNETCORE_URLS into AddExecutable
            // processes, leaving the portal on the default 5000.
            .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http", isProxied: false)
            .WithEnvironment("ASPNETCORE_URLS", "http://localhost:8080")
            // Automatic OTLP env injection targets AddProject resources only — an
            // AddExecutable process needs the explicit opt-in so the portal's T13
            // stack (OtelDefaults reads OTEL_EXPORTER_OTLP_ENDPOINT) exports to
            // this AppHost's dashboard.
            .WithOtlpExporter()
            // Aspire injects ConnectionStrings__Default from the postgres resource
            // (the platform reads ConnectionStrings:Default, not the resource name).
            .WithReference(postgres, connectionName: "Default")
            .WaitFor(postgres)
            .WithEnvironment("Skills__Directory", Path.Combine(repoRoot, "skills"));

        using var app = builder.Build();
        await app.RunAsync();
        return 0;
    }

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "AgentContext.slnx")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
