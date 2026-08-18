using System.Text;
using Yarp.ReverseProxy.Transforms;

namespace AgentContext.Host.DashboardProxy;

/// <summary>
/// Rewrites the Aspire dashboard's Blazor responses so the browser keeps
/// talking to the portal's dashboard prefix (<see cref="DashboardProxySetup.PathPrefix"/>):
///
/// <list type="bullet">
///   <item>HTML gets a rewritten <c>&lt;base href&gt;</c> — the Blazor SPA
///   references assets with relative URLs, so they resolve under the prefix
///   automatically (rewriting individual paths would corrupt nested paths like
///   <c>_content/.../css/...</c>).</item>
///   <item>JavaScript receives root-relative path rewrites for the handful of
///   absolute endpoints Blazor hard-codes (<c>_framework</c>, <c>_content</c>,
///   <c>_blazor</c> circuit URL).</item>
/// </list>
///
/// YARP runs response transforms after the upstream headers have been copied to
/// the client response, so the rewrite swaps in the new content AND drops the
/// stale Content-Length (Kestrel streams the shorter/longer body with chunked
/// encoding instead of truncating or hanging the response). The route asks the
/// dashboard for identity encoding, so bodies arrive uncompressed; a compressed
/// body is left untouched rather than corrupted.
/// </summary>
public sealed class DashboardBodyRewrite : ResponseTransform
{
    private readonly string basePath;
    private readonly string navigationPrefix;

    public DashboardBodyRewrite(
        string basePath = DashboardProxySetup.PathPrefix,
        string navigationPrefix = DashboardProxySetup.PathPrefix)
    {
        this.basePath = basePath.TrimEnd('/') + "/";
        this.navigationPrefix = navigationPrefix.TrimEnd('/');
    }

    public override async ValueTask ApplyAsync(ResponseTransformContext context)
    {
        var response = context.ProxyResponse;
        if (response?.Content is null)
        {
            return;
        }

        // Compressed bytes cannot be string-rewritten; the route requests
        // identity encoding, so this is only a defensive guard.
        var encoding = context.HttpContext.Response.Headers.ContentEncoding.ToString();
        if (!string.IsNullOrEmpty(encoding) && !encoding.Equals("identity", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var mediaType = response.Content.Headers.ContentType?.MediaType ?? "";
        if (!mediaType.Contains("text/html", StringComparison.OrdinalIgnoreCase) &&
            !mediaType.Contains("text/javascript", StringComparison.OrdinalIgnoreCase) &&
            !mediaType.Contains("application/javascript", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(context.CancellationToken);
        var text = Rewrite(mediaType, Encoding.UTF8.GetString(bytes));

        // The upstream Content-Length (already copied to the client response
        // before this transform ran) no longer matches the rewritten body.
        context.HttpContext.Response.ContentLength = null;

        var rewritten = new ByteArrayContent(Encoding.UTF8.GetBytes(text));
        response.Content = rewritten;
    }

    private string Rewrite(string mediaType, string text)
    {
        if (mediaType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
        {
            text = text.Replace("<base href=\"/\"", $"<base href=\"{basePath}\"");
            // The dashboard's nav links are hard-coded root paths that bypass
            // <base href>; navfix.js rewrites them to PathPrefix so Blazor
            // navigates in-place (no full-page redirect / flash) and every
            // dashboard route stays under /monitor.
            return text.Replace(
                "</head>",
                $"<script src=\"/navfix.js?v=2\" data-dashboard-prefix=\"{navigationPrefix}\"></script></head>");
        }

        // JavaScript: only rewrite root-relative Blazor asset/circuit paths.
        foreach (var prefix in new[] { "/_framework", "/_content", "/_blazor" })
        {
            text = text.Replace(prefix, DashboardProxySetup.PathPrefix + prefix);
        }

        return text;
    }
}
