using AgentContext.Application.Sessions;
using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Domain;
using DomainEntity = AgentContext.Domain.Entities.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using AgentContext.Tests.Testcontainers;
using Microsoft.EntityFrameworkCore;

namespace AgentContext.Tests.SeamTests;

/// <summary>
/// Primary seam — Session recording at the application service boundary against a
/// real Postgres (spec §Testing Decisions). Covers T2 acceptance: Session row +
/// Usage, explicit domain tagging with inference fallback, remember semantics.
/// </summary>
public sealed class SaveSessionTests : PostgresTestBase
{
    private async Task<(AgentContextDbContext Db, ISaveSessionAppService Service)> SeededAsync()
    {
        var db = Fixture.CreateDbContext();
        await db.Database.MigrateAsync();

        // T1 first-run state: one admin user + personal workspace (+ a "dev" domain).
        var workspace = new Workspace { Name = "Danvic's Workspace", Type = WorkspaceType.Personal };
        db.Workspaces.Add(workspace);
        db.Domains.Add(new DomainEntity { WorkspaceId = workspace.Id, Name = "dev", IsShared = false });
        await db.SaveChangesAsync();

        return (db, new SaveSessionAppService(db));
    }

    [Fact]
    public async Task Save_creates_pending_session_with_usage_attached()
    {
        var (db, service) = await SeededAsync();

        var result = await service.SaveAsync(new SaveSessionRequest(
            Domain: "dev",
            Task: "Fix pgvector index",
            Conclusion: "hnsw index works",
            KeySnippets: ["vector_l2_ops", "ef_construction=64"],
            AgentName: "craft-agents",
            Model: "gpt-4o",
            TokensIn: 1200,
            TokensOut: 800,
            Cost: 0.42m));

        Assert.NotEqual(Guid.Empty, result.SessionId);
        Assert.Equal("dev", result.DomainName);
        Assert.False(result.Remembered);

        var session = await db.Sessions.SingleAsync(s => s.Id == result.SessionId);
        Assert.Equal(SessionStatus.Pending, session.Status);
        Assert.Equal("craft-agents", session.AgentName);
        Assert.False(session.Remembered);
        Assert.Null(session.FullContext);
        Assert.Contains("hnsw index works", session.SummaryJson);

        var usage = await db.Usage.SingleAsync(u => u.SessionId == session.Id);
        Assert.Equal("gpt-4o", usage.Model);
        Assert.Equal(1200, usage.TokensIn);
        Assert.Equal(800, usage.TokensOut);
        Assert.Equal(0.42m, usage.Cost);
    }

    [Fact]
    public async Task Save_uses_existing_domain()
    {
        var (db, service) = await SeededAsync();

        await service.SaveAsync(new SaveSessionRequest(Domain: "dev", Task: "t", Conclusion: "c"));
        var session = await db.Sessions.SingleAsync();

        Assert.NotNull(session.DomainId);
        var domain = await db.Domains.SingleAsync(d => d.Id == session.DomainId);
        Assert.Equal("dev", domain.Name);
        Assert.Single(await db.Domains.ToListAsync()); // no new domain created
    }

    [Fact]
    public async Task Save_registers_unknown_domain_as_inference()
    {
        var (db, service) = await SeededAsync();

        await service.SaveAsync(new SaveSessionRequest(Domain: "home", Task: "t", Conclusion: "c"));

        var domain = await db.Domains.SingleAsync(d => d.Name == "home");
        Assert.False(domain.IsShared);
        var session = await db.Sessions.SingleAsync();
        Assert.Equal(domain.Id, session.DomainId);
    }

    [Fact]
    public async Task Save_without_domain_leaves_domain_null()
    {
        var (db, service) = await SeededAsync();

        await service.SaveAsync(new SaveSessionRequest(Domain: null, Task: "t", Conclusion: "c"));

        var session = await db.Sessions.SingleAsync();
        Assert.Null(session.DomainId);
    }

    [Fact]
    public async Task Remember_stores_full_context_otherwise_only_summary()
    {
        var (db, service) = await SeededAsync();

        await service.SaveAsync(new SaveSessionRequest(
            Domain: "dev", Task: "t", Conclusion: "c",
            Remembered: true, FullContext: "the entire conversation..."));

        var remembered = await db.Sessions.SingleAsync();
        Assert.True(remembered.Remembered);
        Assert.Equal("the entire conversation...", remembered.FullContext);

        // A second, non-remembered session on the same database must not carry full context.
        await service.SaveAsync(new SaveSessionRequest(Domain: "dev", Task: "t2", Conclusion: "c2"));

        var plain = await db.Sessions.SingleAsync(s => s.Task == "t2");
        Assert.False(plain.Remembered);
        Assert.Null(plain.FullContext);
    }

    [Fact]
    public async Task Remember_requires_full_context()
    {
        var (db, service) = await SeededAsync();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SaveAsync(new SaveSessionRequest(Domain: "dev", Task: "t", Conclusion: "c", Remembered: true)));
    }

    [Fact]
    public async Task Save_keeps_usage_when_model_missing_but_tokens_reported()
    {
        var (db, service) = await SeededAsync();

        await service.SaveAsync(new SaveSessionRequest(
            Domain: "dev", Task: "t", Conclusion: "c", TokensIn: 200, TokensOut: 100));

        var session = await db.Sessions.Include(s => s.Usage).SingleAsync();
        var usage = Assert.Single(session.Usage);
        Assert.Equal("unknown", usage.Model);
        Assert.Equal(300, usage.TokensIn + usage.TokensOut);
    }

    [Fact]
    public async Task Save_throws_when_platform_not_configured()
    {
        await using var db = Fixture.CreateDbContext();
        await db.Database.MigrateAsync(); // no workspace seeded
        var service = new SaveSessionAppService(db);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.SaveAsync(new SaveSessionRequest(Domain: "dev", Task: "t", Conclusion: "c")));
    }

    [Fact]
    public async Task Get_and_list_expose_usage_for_session_overview()
    {
        var (db, service) = await SeededAsync();
        var result = await service.SaveAsync(new SaveSessionRequest(
            Domain: "dev", Task: "t", Conclusion: "c",
            Model: "gpt-4o", TokensIn: 1000, TokensOut: 500, Cost: 0.1m));

        var detail = await service.GetAsync(result.SessionId);
        Assert.Equal("dev", detail.DomainName);
        Assert.Equal("craft-agents", detail.AgentName); // default agent name
        var usage = Assert.Single(detail.Usage);
        Assert.Equal(1500, usage.TokensIn + usage.TokensOut);

        var list = await service.ListAsync();
        var item = Assert.Single(list);
        Assert.Equal(1500, item.TotalTokens);
        Assert.Equal(0.1m, item.TotalCost);
        Assert.Equal("Pending", item.Status);
    }
}
