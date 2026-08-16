using AgentContext.Application.Analytics;
using AgentContext.Application.Dtos;
using AgentContext.Application.Pricing;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using AgentContext.Tests.Testcontainers;
using Microsoft.EntityFrameworkCore;
using DomainEntity = AgentContext.Domain.Entities.Domain;

namespace AgentContext.Tests.SeamTests;

/// <summary>
/// Session overview analytics (T7, issue #8 / spec US27–28): sessions / tokens /
/// cost by domain and agent, filtered, with cost computed from the maintained
/// pricing table. Primary seam against Testcontainers pgvector.
/// </summary>
public sealed class AnalyticsTests : PostgresTestBase
{
    private async Task<AgentContextDbContext> SeedAsync()
    {
        var db = Fixture.CreateDbContext();
        await db.Database.MigrateAsync();

        var workspace = new Workspace { Name = "W", Type = WorkspaceType.Personal };
        var dev = new DomainEntity { WorkspaceId = workspace.Id, Name = "dev", IsShared = false };
        var home = new DomainEntity { WorkspaceId = workspace.Id, Name = "home", IsShared = false };
        db.Workspaces.Add(workspace);
        db.Domains.AddRange(dev, home);

        var s1 = new Session
        {
            WorkspaceId = workspace.Id, DomainId = dev.Id, AgentName = "agent-a",
            Task = "t1", Conclusion = "c", SummaryJson = "{}", Status = SessionStatus.Completed,
        };
        s1.Usage.Add(new Usage { Model = "gpt-4o", TokensIn = 1000, TokensOut = 200, CreatedAtUtc = DateTimeOffset.UtcNow });
        var s2 = new Session
        {
            WorkspaceId = workspace.Id, DomainId = dev.Id, AgentName = "agent-a",
            Task = "t2", Conclusion = "c", SummaryJson = "{}", Status = SessionStatus.Completed,
        };
        s2.Usage.Add(new Usage { Model = "gpt-4o", TokensIn = 500, TokensOut = 100, CreatedAtUtc = DateTimeOffset.UtcNow });
        var s3 = new Session
        {
            WorkspaceId = workspace.Id, DomainId = home.Id, AgentName = "agent-b",
            Task = "t3", Conclusion = "c", SummaryJson = "{}", Status = SessionStatus.Completed,
        };
        s3.Usage.Add(new Usage { Model = "claude-3.5", TokensIn = 2000, TokensOut = 300, CreatedAtUtc = DateTimeOffset.UtcNow });
        db.Sessions.AddRange(s1, s2, s3);

        await db.SaveChangesAsync();
        return db;
    }

    private static AnalyticsAppService Service(AgentContextDbContext db) => new(db, new PricingAppService(db));

    [Fact]
    public async Task Overview_totals_sessions_tokens_and_priced_cost()
    {
        await using var db = await SeedAsync();
        await new PricingAppService(db).SaveAsync(new SaveModelPricingRequest("gpt-4o", 0.0000025m, 0.00001m));

        var overview = await Service(db).GetOverviewAsync();

        // 3 sessions, 1000+500+2000 in / 200+100+300 out.
        Assert.Equal(3, overview.TotalSessions);
        Assert.Equal(3500, overview.TotalTokensIn);
        Assert.Equal(600, overview.TotalTokensOut);
        // gpt-4o: 1500×0.0000025 + 300×0.00001 = 0.00375 + 0.003 = 0.00675.
        // claude-3.5 has no pricing row → cost 0.
        Assert.Equal(0.00675m, overview.TotalCost, 10);
    }

    [Fact]
    public async Task Overview_breaks_down_by_domain_and_agent()
    {
        await using var db = await SeedAsync();
        await new PricingAppService(db).SaveAsync(new SaveModelPricingRequest("gpt-4o", 0.0000025m, 0.00001m));

        var overview = await Service(db).GetOverviewAsync();

        var dev = Assert.Single(overview.ByDomain, g => g.Name == "dev");
        Assert.Equal(2, dev.Sessions);
        Assert.Equal(1500, dev.TokensIn);
        Assert.Equal(300, dev.TokensOut);
        Assert.Equal(0.00675m, dev.Cost, 10);

        var home = Assert.Single(overview.ByDomain, g => g.Name == "home");
        Assert.Equal(1, home.Sessions);
        Assert.Equal(0, home.Cost);

        var agentA = Assert.Single(overview.ByAgent, g => g.Name == "agent-a");
        Assert.Equal(2, agentA.Sessions);
        Assert.Equal(0.00675m, agentA.Cost, 10);

        var agentB = Assert.Single(overview.ByAgent, g => g.Name == "agent-b");
        Assert.Equal(1, agentB.Sessions);
    }

    [Fact]
    public async Task Overview_filters_by_domain_and_agent()
    {
        await using var db = await SeedAsync();
        await new PricingAppService(db).SaveAsync(new SaveModelPricingRequest("gpt-4o", 0.0000025m, 0.00001m));

        var byDomain = await Service(db).GetOverviewAsync(domain: "dev");
        Assert.Equal(2, byDomain.TotalSessions);
        Assert.Equal(1500, byDomain.TotalTokensIn);
        Assert.Single(byDomain.ByDomain, g => g.Name == "dev");

        var byAgent = await Service(db).GetOverviewAsync(agent: "agent-b");
        Assert.Equal(1, byAgent.TotalSessions);
        Assert.Equal(2000, byAgent.TotalTokensIn);
        Assert.Single(byAgent.ByAgent, g => g.Name == "agent-b");

        var both = await Service(db).GetOverviewAsync(domain: "dev", agent: "agent-b");
        Assert.Equal(0, both.TotalSessions);
    }

    [Fact]
    public async Task Overview_sessions_without_usage_count_but_cost_zero()
    {
        await using var db = await SeedAsync();
        var workspace = await db.Workspaces.AsNoTracking().SingleAsync();
        db.Sessions.Add(new Session
        {
            WorkspaceId = workspace.Id, AgentName = "agent-c",
            Task = "t4", Conclusion = "c", SummaryJson = "{}", Status = SessionStatus.Completed,
        });
        await db.SaveChangesAsync();
        await new PricingAppService(db).SaveAsync(new SaveModelPricingRequest("gpt-4o", 0.0000025m, 0.00001m));

        var overview = await Service(db).GetOverviewAsync();

        Assert.Equal(4, overview.TotalSessions);
        var agentC = Assert.Single(overview.ByAgent, g => g.Name == "agent-c");
        Assert.Equal(1, agentC.Sessions);
        Assert.Equal(0, agentC.TokensIn);
        Assert.Equal(0, agentC.Cost);
    }
}
