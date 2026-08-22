using AgentContext.Application.Analytics;
using AgentContext.Application.Dtos;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using AgentContext.Application.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Moq;
using DomainEntity = AgentContext.Domain.Entities.Domain;

namespace AgentContext.Application.Tests.Analytics;

public sealed class AnalyticsAppServiceTests
{
    [Fact]
    public async Task GetOverview_summarizes_sessions_and_tokens_without_cost_data()
    {
        var workspace = new Workspace { Name = "Personal", Type = WorkspaceType.Personal };
        var domain = new DomainEntity
        {
            WorkspaceId = workspace.Id,
            Name = "dev",
            IsShared = false,
        };
        var session = new Session
        {
            WorkspaceId = workspace.Id,
            DomainId = domain.Id,
            Domain = domain,
            AgentName = "craft-agents",
            Usage =
            [
                new Usage
                {
                    Model = "gpt-4o-mini",
                    InputTokens = 100,
                    CachedInputTokens = 20,
                    OutputTokens = 40,
                    Source = UsageSource.ReportedSession,
                },
            ],
        };
        var db = new Mock<AgentContextDbContext>(new DbContextOptions<AgentContextDbContext>());
        db.SetupGet(context => context.Workspaces).Returns(MockDbSetFactory.Create([workspace]));
        db.SetupGet(context => context.Sessions).Returns(MockDbSetFactory.Create([session]));
        var service = new AnalyticsAppService(db.Object);

        var result = await service.GetOverviewAsync();

        Assert.Equal(1, result.TotalSessions);
        Assert.Equal(100, result.TotalTokensIn);
        Assert.Equal(40, result.TotalTokensOut);
        Assert.Single(result.ByDomain);
        Assert.Equal("dev", result.ByDomain[0].Name);
        Assert.Equal(100, result.ByDomain[0].TokensIn);
        Assert.Equal(40, result.ByDomain[0].TokensOut);
        Assert.Single(result.ByAgent);
        Assert.Equal("craft-agents", result.ByAgent[0].Name);
    }
}
