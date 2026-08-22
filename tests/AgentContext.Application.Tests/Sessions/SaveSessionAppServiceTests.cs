using System.Net;
using AgentContext.Application.Dtos;
using AgentContext.Application.Localization;
using AgentContext.Application.Sessions;
using AgentContext.Application.Tests.TestSupport;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AgentContext.Application.Tests.Sessions;

public sealed class SaveSessionAppServiceTests
{
    [Fact]
    public async Task Save_persists_the_reported_usage_snapshot_without_route_resolution()
    {
        var savedSessions = new List<Session>();
        var service = CreateService(savedSessions);
        var usage = new SessionUsageInput("vendor/model-v2", 1_200, 300, 800);

        await service.SaveAsync(new SaveSessionRequest(
            Domain: null,
            Task: "task",
            Conclusion: "conclusion",
            Usage: usage));

        var session = Assert.Single(savedSessions);
        var persisted = Assert.Single(session.Usage);
        Assert.Equal(usage.Model, persisted.Model);
        Assert.Equal(usage.InputTokens, persisted.InputTokens);
        Assert.Equal(usage.CachedInputTokens, persisted.CachedInputTokens);
        Assert.Equal(usage.OutputTokens, persisted.OutputTokens);
        Assert.Equal(UsageSource.ReportedSession, persisted.Source);
        Assert.Null(persisted.InferenceRouteId);
        Assert.Null(persisted.Capability);
    }

    [Fact]
    public async Task Save_without_usage_does_not_create_a_usage_row()
    {
        var savedSessions = new List<Session>();
        var service = CreateService(savedSessions);

        await service.SaveAsync(new SaveSessionRequest(
            Domain: null,
            Task: "task",
            Conclusion: "conclusion"));

        Assert.Empty(Assert.Single(savedSessions).Usage);
    }

    [Fact]
    public async Task Save_persists_a_reported_usage_payload_even_when_all_counts_are_zero()
    {
        var savedSessions = new List<Session>();
        var service = CreateService(savedSessions);

        await service.SaveAsync(new SaveSessionRequest(
            Domain: null,
            Task: "task",
            Conclusion: "conclusion",
            Usage: new SessionUsageInput("external-model", 0, 0, 0)));

        Assert.Single(Assert.Single(savedSessions).Usage);
    }

    [Theory]
    [InlineData(-1, 0, 0)]
    [InlineData(0, -1, 0)]
    [InlineData(0, 0, -1)]
    [InlineData(10, 11, 0)]
    public async Task Save_rejects_invalid_reported_usage_counts(int input, int cachedInput, int output)
    {
        var service = CreateService([]);

        var exception = await Assert.ThrowsAsync<LocalizedException>(() => service.SaveAsync(
            new SaveSessionRequest(
                Domain: null,
                Task: "task",
                Conclusion: "conclusion",
                Usage: new SessionUsageInput("external-model", input, cachedInput, output))));

        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal(ErrorCodes.Session.UsageInvalid, exception.ErrorCode);
    }

    [Fact]
    public void Save_session_request_contains_only_the_shared_usage_payload()
    {
        var propertyNames = typeof(SaveSessionRequest)
            .GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);

        Assert.Contains(nameof(SaveSessionRequest.Usage), propertyNames);
        Assert.DoesNotContain("Model", propertyNames);
        Assert.DoesNotContain("TokensIn", propertyNames);
        Assert.DoesNotContain("TokensOut", propertyNames);
        Assert.DoesNotContain("Cost", propertyNames);
    }

    private static SaveSessionAppService CreateService(ICollection<Session> savedSessions)
    {
        var options = new DbContextOptionsBuilder<AgentContextDbContext>().Options;
        var db = new Mock<AgentContextDbContext>(MockBehavior.Strict, options);
        db.SetupGet(context => context.Workspaces)
            .Returns(MockDbSetFactory.Create([
                new Workspace { Name = "workspace", Type = WorkspaceType.Personal },
            ]));

        var sessions = new Mock<DbSet<Session>>();
        sessions.Setup(set => set.Add(It.IsAny<Session>()))
            .Callback<Session>(savedSessions.Add);
        db.SetupGet(context => context.Sessions).Returns(sessions.Object);
        db.Setup(context => context.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        return new SaveSessionAppService(db.Object);
    }
}
