using AgentContext.Host.DashboardProxy;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Yarp.ReverseProxy.Configuration;

namespace AgentContext.Tests.AdapterTests;

public sealed class DashboardProxyTests
{
    [Theory]
    [InlineData("/resources", "/monitor/resources")]
    [InlineData("/resources/abc", "/monitor/resources/abc")]
    [InlineData("/metrics", "/monitor/metrics")]
    public void Dashboard_root_pages_are_mapped_to_the_portal_prefix(string path, string expected)
    {
        Assert.True(DashboardProxySetup.TryGetDashboardPrefixPath(new PathString(path), out var target));
        Assert.Equal(expected, target);
    }

    [Fact]
    public void Portal_root_is_not_claimed_by_dashboard_redirect()
    {
        Assert.False(DashboardProxySetup.TryGetDashboardPrefixPath(new PathString("/"), out _));
    }

    [Fact]
    public void Canonical_resources_route_proxies_to_the_dashboard_root()
    {
        var services = new ServiceCollection();
        services.AddDashboardProxy("http://localhost:18888");
        using var provider = services.BuildServiceProvider();

        var config = provider.GetRequiredService<IProxyConfigProvider>().GetConfig();
        var route = Assert.Single(config.Routes, item => item.RouteId == DashboardProxySetup.RouteId + "-resources");

        Assert.Equal(DashboardProxySetup.PathPrefix + DashboardProxySetup.DefaultPagePath, route.Match.Path);
        Assert.Contains(route.Transforms!, transform =>
            transform.TryGetValue("PathSet", out var path) && path == "/");
    }
}
