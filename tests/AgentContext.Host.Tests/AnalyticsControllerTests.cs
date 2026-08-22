using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Host.Controllers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Moq;
using System.Reflection;

namespace AgentContext.Host.Tests;

public sealed class AnalyticsControllerTests
{
    [Fact]
    public async Task Overview_returns_the_token_only_contract()
    {
        var analytics = new Mock<IAnalyticsAppService>(MockBehavior.Strict);
        var overview = new AnalyticsOverview(
            1,
            100,
            40,
            [new AnalyticsGroupItem("dev", 1, 100, 40)],
            [new AnalyticsGroupItem("craft-agents", 1, 100, 40)]);
        analytics.Setup(service => service.GetOverviewAsync(null, null, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(overview);
        var controller = new AnalyticsController(analytics.Object);

        var result = await controller.Overview(cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Same(overview, ok.Value);
    }

    [Fact]
    public void Controller_has_no_pricing_routes()
    {
        var actions = typeof(AnalyticsController)
            .GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly);

        Assert.DoesNotContain(
            actions.SelectMany(method => method.GetCustomAttributes<HttpMethodAttribute>()),
            attribute => attribute.Template?.StartsWith("pricing", StringComparison.OrdinalIgnoreCase) == true);
    }
}
