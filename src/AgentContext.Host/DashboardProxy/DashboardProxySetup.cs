using Yarp.ReverseProxy.Configuration;
using Yarp.ReverseProxy.Transforms.Builder;

namespace AgentContext.Host.DashboardProxy;

/// <summary>
/// Single-port model (issue #15): serves the in-process Aspire dashboard on the
/// portal's :8080 under <see cref="PathPrefix"/> via YARP, so users only open
/// one URL. Blazor's websocket circuit and SignalR long-polling are handled
/// natively by YARP; the <see cref="DashboardBodyRewrite"/> transform fixes the
/// SPA's root-relative asset paths, and the header transforms stop the browser
/// from 304-revalidating stale rewritten content.
///
/// Two route families point at the same cluster:
/// <list type="bullet">
///   <item><c>{PathPrefix}/*</c> — the primary entry; the prefix is stripped and
///   text bodies are rewritten for the prefix.</item>
///   <item>dashboard root paths (see <see cref="RootPaths"/>) — the dashboard's
///   Blazor runtime references root-absolute URLs that bypass
///   <c>&lt;base href&gt;</c> (dynamically imported module scripts, nav links),
///   so those must proxy to the dashboard too. They are not rewritten: their own
///   (root-relative) imports resolve through the same root routes, staying
///   self-consistent.</item>
/// </list>
///
/// Gated on <c>DASHBOARD_ORIGIN</c> (the AppHost injects it into the portal
/// child process). The YARP surface is still registered when the origin is
/// absent — Program.cs calls <c>MapReverseProxy()</c> unconditionally — it just
/// has zero routes, so the prefix falls through to the SPA fallback.
/// </summary>
public static class DashboardProxySetup
{
    public const string OriginConfigKey = "DASHBOARD_ORIGIN";

    /// <summary>URL prefix the dashboard is served under on the portal.</summary>
    public const string PathPrefix = "/monitor";

    /// <summary>
    /// Root-path dashboard pages whose nav links (e.g. the dashboard home link
    /// <c>href="/"</c>) navigate back to <c>/</c>; used to tell dashboard-
    /// originated requests apart from plain portal visits.
    /// </summary>
    private static readonly string[] DashboardPagePrefixes = ["/resources", "/consolelogs", "/structuredlogs", "/traces", "/metrics", "/login"];

    public const string RouteId = "dashboard-proxy";
    public const string ClusterId = "dashboard-cluster";

    /// <summary>
    /// Dashboard root paths the Blazor runtime references with root-absolute URLs
    /// that bypass <c>&lt;base href&gt;</c>: dynamically imported module scripts
    /// (e.g. <c>/js/app-theme.js</c>), the module chain they import
    /// (<c>/ _content/...</c>), the Blazor circuit, and a handful of top-level
    /// assets (<c>Aspire.Dashboard.styles.css</c>, <c>favicon.ico</c>). The
    /// portal ships no files under these prefixes, so proxying them is
    /// conflict-free.
    ///
    /// These routes do NOT run the body rewrite: they serve raw assets whose
    /// root-relative imports resolve through these same routes, staying
    /// self-consistent. Dashboard <em>pages</em> are deliberately not routed
    /// here — the nav links navigate to root paths (/consolelogs, /metrics, ...),
    /// and the portal redirects those to <see cref="PathPrefix"/> so every
    /// dashboard route stays under /monitor (see <see cref="TryGetDashboardPrefixPath"/>).
    /// </summary>
    private static readonly (string Id, string Path)[] RootPaths =
    [
        ("js", "/js/{**catch-all}"),
        ("css", "/css/{**catch-all}"),
        ("framework", "/framework/{**catch-all}"),
        ("content", "/_content/{**catch-all}"),
        ("blazor", "/_blazor/{**catch-all}"),
        ("styles", "/Aspire.Dashboard.styles.css"),
        ("favicon", "/favicon.ico"),
    ];

    /// <summary>
    /// Maps a dashboard root-path page URL to its <see cref="PathPrefix"/>
    /// equivalent (keeping the query string), so every dashboard route stays
    /// under /monitor. Returns false for non-page paths (the portal owns those).
    /// </summary>
    public static bool TryGetDashboardPrefixPath(PathString path, out string target)
    {
        target = "";
        var value = path.Value;
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        foreach (var prefix in DashboardPagePrefixes)
        {
            if (value == prefix || value.StartsWith(prefix + "/", StringComparison.Ordinal))
            {
                target = PathPrefix + value;
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Transforms shared by every route: identity encoding (so the body rewrite
    /// and header transforms work on plain text) and no-store caching.
    /// </summary>
    private static readonly IReadOnlyList<IReadOnlyDictionary<string, string>> SharedTransforms =
    [
        new Dictionary<string, string> { ["RequestHeader"] = "Accept-Encoding", ["Set"] = "identity" },
        // Never cache proxied dashboard content: rewritten responses must never
        // be 304-revalidated, and the ETag/Last-Modified validators no longer
        // describe the body once it has been rewritten.
        new Dictionary<string, string> { ["ResponseHeader"] = "Cache-Control", ["Set"] = "no-store", ["When"] = "Always" },
        new Dictionary<string, string> { ["ResponseHeader"] = "Expires", ["Set"] = "0", ["When"] = "Always" },
        new Dictionary<string, string> { ["ResponseHeaderRemove"] = "ETag", ["When"] = "Always" },
        new Dictionary<string, string> { ["ResponseHeaderRemove"] = "Last-Modified", ["When"] = "Always" },
        new Dictionary<string, string> { ["ResponseHeaderRemove"] = "Accept-Ranges", ["When"] = "Always" },
    ];

    public static void AddDashboardProxy(this IServiceCollection services, string? dashboardOrigin)
    {
        if (string.IsNullOrWhiteSpace(dashboardOrigin))
        {
            // No origin (standalone portal / tests): YARP with no routes. Keeps
            // Program.cs's unconditional MapReverseProxy() from throwing.
            services.AddReverseProxy().LoadFromMemory([], []);
            return;
        }

        var origin = dashboardOrigin.TrimEnd('/');

        var routes = new[]
            {
                // The prefix itself (no trailing segment): the prefix removal
                // leaves an empty path, which the HttpClient normalizes to "/".
                new RouteConfig
                {
                    RouteId = RouteId + "-root",
                    ClusterId = ClusterId,
                    Match = new RouteMatch { Path = PathPrefix },
                    Transforms = PrefixTransforms(),
                },
                new RouteConfig
                {
                    RouteId = RouteId,
                    ClusterId = ClusterId,
                    Match = new RouteMatch { Path = PathPrefix + "/{**catch-all}" },
                    Transforms = PrefixTransforms(),
                },
            }
            .Concat(RootPaths.Select(r => RootRoute(r.Id, r.Path)))
            .ToArray();

        var clusters = new[]
        {
            new ClusterConfig
            {
                ClusterId = ClusterId,
                Destinations = new Dictionary<string, DestinationConfig>
                {
                    ["dashboard"] = new() { Address = origin },
                },
            },
        };

        services.AddReverseProxy()
            .LoadFromMemory(routes, clusters);
        services.AddSingleton<ITransformFactory, DashboardBodyRewriteFactory>();
    }

    private static IReadOnlyList<IReadOnlyDictionary<string, string>> PrefixTransforms() =>
    [
        // {PathPrefix}/x -> /x (and {PathPrefix} -> /).
        new Dictionary<string, string> { ["PathRemovePrefix"] = PathPrefix },
        ..SharedTransforms,
        // Custom transform (registered via DashboardBodyRewriteFactory): rewrites
        // the Blazor HTML/JS bodies for the prefix.
        new Dictionary<string, string> { ["DashboardBodyRewrite"] = "true" },
    ];

    private static IReadOnlyList<IReadOnlyDictionary<string, string>> RootPathTransforms() =>
    [
        ..SharedTransforms,
    ];

    private static RouteConfig RootRoute(string id, string path) => new()
    {
        RouteId = RouteId + "-" + id,
        ClusterId = ClusterId,
        Match = new RouteMatch { Path = path },
        Transforms = RootPathTransforms(),
    };
}

/// <summary>Wires the <c>DashboardBodyRewrite</c> transform name to its class.</summary>
public sealed class DashboardBodyRewriteFactory : ITransformFactory
{
    public bool Validate(TransformRouteValidationContext context, IReadOnlyDictionary<string, string> transformValues)
        => transformValues.ContainsKey("DashboardBodyRewrite");

    public bool Build(TransformBuilderContext context, IReadOnlyDictionary<string, string> transformValues)
    {
        if (transformValues.TryGetValue("DashboardBodyRewrite", out _))
        {
            context.ResponseTransforms.Add(new DashboardBodyRewrite());
            return true;
        }

        return false;
    }
}
