using System.Reflection;
using AgentContext.Host.DashboardProxy;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Postgres;

namespace AgentContext.Host.AppHost;

/// <summary>
/// The default entrypoint (no args): runs this same binary as an Aspire
/// DistributedApplication so the dashboard gains the Resources / service view
/// that the standalone (compose) dashboard lacks — the resource service is
/// AppHost-only.
///
/// Postgres is deliberately <b>not</b> orchestrated when the environment
/// already carries <c>ConnectionStrings__Default</c> (the container image /
/// compose case, issue #15): the connection string is modelled as an
/// <i>external</i> resource (<see cref="ConnectionStringBuilderExtensions.AddConnectionString"/>)
/// and passed straight to the portal child process — no container to start,
/// so the image itself is complete (UI + MCP + in-process dashboard) without
/// a bundled database. Bare <c>dotnet run</c> local dev (no env) keeps the
/// pgvector container path so one command still brings up everything.
///
/// The dashboard is bound to a <b>fixed internal port</b> (default 18888,
/// override via <c>DASHBOARD_PORT</c>) so the portal's reverse proxy can reach
/// it consistently; browser traffic uses the portal's <c>/monitor</c> surface.
/// The portal is a child process of this binary, narrowed to the portal host
/// via the internal <c>HOST_MODE=portal</c> role. The dashboard is hosted
/// in-process by Aspire; the portal opts into OTLP export via
/// <c>WithOtlpExporter()</c> (AddExecutable resources aren't covered by the
/// automatic env injection), which the portal's T13 stack
/// (<see cref="Observability.OtelDefaults"/>, reads <c>OTEL_EXPORTER_OTLP_ENDPOINT</c>)
/// picks up as-is — logs/traces/metrics flow to the same dashboard that shows
/// the Resources view.
/// </summary>
public static class AppHostRunner
{
    /// <summary>Fixed internal dashboard port used by the portal proxy.</summary>
    public const string DefaultDashboardPort = "18888";

    public static async Task<int> RunAsync(string[] args)
    {
        // Local development / unsecured transport: the in-process dashboard
        // serves plain HTTP and skips frontend auth so the UI's "open dashboard"
        // menu lands straight on it (like the old compose standalone container).
        // ASPIRE_ALLOW_UNSECURED_TRANSPORT is read by the AppHost process itself,
        // so a plain env var works. For the dashboard's own auth mode, DCP
        // explicitly injects Dashboard:Frontend:AuthMode=BrowserToken into the
        // dashboard process (overriding any inherited env), so the only reliable
        // switch is the official shortcut env ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS
        // — the AppHost reads it and configures all three dashboard auth modes
        // (Frontend/Otlp/Api) as Unsecured.
        Environment.SetEnvironmentVariable("ASPIRE_ALLOW_UNSECURED_TRANSPORT", "true");
        Environment.SetEnvironmentVariable("ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS", "true");

        var builder = DistributedApplication.CreateBuilder(args);

        // The dashboard is its own process started by DCP. Pin its listening port
        // (default 18888, override via DASHBOARD_PORT) by writing the URL into the
        // AppHost configuration — DCP reads ASPNETCORE_URLS from the AppHost
        // config when it generates the dashboard process environment (a bare
        // Environment.SetEnvironmentVariable on this process is overridden by DCP
        // with a dynamic port). "http://+:" (not localhost) so host port-mapping
        // reaches it from outside the container.
        var dashboardPort = Environment.GetEnvironmentVariable("DASHBOARD_PORT") ?? DefaultDashboardPort;
        builder.Configuration["ASPNETCORE_URLS"] = $"http://+:{dashboardPort}";

        var repoRoot = FindRepoRoot();
        var baseDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location)!;

        // Deployment surface is the authority (T15 follow-up): the container
        // image / compose carries Skills__Directory (data volume) and
        // DASHBOARD_URL (browser-facing dashboard) as env vars. Respect
        // pre-set values; bare `dotnet run` local dev falls back to the
        // repo-local defaults below. (Unconditionally overriding them here made
        // the compose volume dead config — skills were written to the container's
        // ephemeral layer and lost on recreation.)
        var skillsDirectory = Environment.GetEnvironmentVariable("Skills__Directory");
        if (string.IsNullOrWhiteSpace(skillsDirectory))
        {
            skillsDirectory = Path.Combine(repoRoot, "skills");
        }

        var dashboardUrl = Environment.GetEnvironmentVariable("DASHBOARD_URL");
        if (string.IsNullOrWhiteSpace(dashboardUrl))
        {
            // Browser-facing dashboard URL for the UI's "open dashboard" menu
            // entry: the portal's own reverse-proxy prefix, so both the UI and
            // the dashboard share one URL (:8080). The raw dashboard port stays
            // container-internal (single-port model).
            dashboardUrl = $"http://localhost:8080{DashboardProxySetup.PathPrefix}{DashboardProxySetup.DefaultPagePath}";
        }

        // Issue #15: when a connection string is already provided (container /
        // compose), model postgres as an external resource instead of starting a
        // container — the portal child gets ConnectionStrings__Default injected.
        var externalConnectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Default");
        var externalPostgres = !string.IsNullOrWhiteSpace(externalConnectionString);

        IResourceBuilder<IResourceWithConnectionString> postgres;
        if (externalPostgres)
        {
            // Value is read from configuration "ConnectionStrings:Default"
            // (i.e. the ConnectionStrings__Default env var) at build time.
            postgres = builder.AddConnectionString("Default");
        }
        else
        {
            // Bare `dotnet run` local dev: one command brings up postgres too.
            // Fixed password (not Aspire's auto-generated one): a named data volume
            // is initialized with the password seen on first start, so a regenerated
            // password on the next run would fail authentication against the old volume.
            var pgPassword = builder.AddParameter("postgres-password", "agent_context");

            postgres = builder.AddPostgres("postgres", password: pgPassword)
                .WithImage("pgvector/pgvector")
                .WithImageTag("pg17")
                .WithDataVolume("agentcontext-pgdata");
        }

        var portal = builder.AddExecutable("portal", "dotnet", baseDir, "AgentContext.Host.dll")
            // The child process must run only the portal host (no nested
            // orchestration); the env var is the deployment seam, not a flag.
            .WithEnvironment("HOST_MODE", "portal")
            // Non-proxied so the portal binds 8080 directly (same surface as compose);
            // DCP's proxied endpoints don't inject ASPNETCORE_URLS into AddExecutable
            // processes, leaving the portal on the default 5000.
            .WithHttpEndpoint(port: 8080, targetPort: 8080, name: "http", isProxied: false)
            // "+:" when postgres is external (container image — host port-mapping
            // needs a non-loopback bind); localhost for bare `dotnet run` dev.
            .WithEnvironment("ASPNETCORE_URLS", externalPostgres ? "http://+:8080" : "http://localhost:8080")
            // Automatic OTLP env injection targets AddProject resources only — an
            // AddExecutable process needs the explicit opt-in so the portal's T13
            // stack (OtelDefaults reads OTEL_EXPORTER_OTLP_ENDPOINT) exports to
            // this AppHost's dashboard.
            .WithOtlpExporter()
            .WithEnvironment("DASHBOARD_URL", dashboardUrl)
            // Internal dashboard origin: lets the portal's reverse proxy serve the
            // dashboard on the same :8080 URL (single-port model), so the
            // dashboard port stays container-internal.
            .WithEnvironment("DASHBOARD_ORIGIN", $"http://localhost:{dashboardPort}")
            // Aspire injects ConnectionStrings__Default from the postgres resource
            // (the platform reads ConnectionStrings:Default, not the resource name).
            .WithReference(postgres, connectionName: "Default")
            .WithEnvironment("Skills__Directory", skillsDirectory);

        // Only the containerized postgres needs a readiness wait; an external
        // connection-string resource is already "there" by definition.
        if (!externalPostgres)
        {
            portal.WaitFor(postgres);
        }

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
