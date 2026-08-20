using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AgentContext.Application.Tests.TestSupport;

internal static class MockInferenceDbContext
{
    public static Mock<AgentContextDbContext> Create(
        IEnumerable<InferenceConfiguration>? configurations = null,
        IEnumerable<InferenceProvider>? providers = null,
        IEnumerable<InferenceRoute>? routes = null,
        IEnumerable<AppSetting>? settings = null)
    {
        var options = new DbContextOptionsBuilder<AgentContextDbContext>().Options;
        var context = new Mock<AgentContextDbContext>(MockBehavior.Strict, options);
        context.SetupGet(db => db.InferenceConfigurations)
            .Returns(MockDbSetFactory.Create(configurations ?? []));
        context.SetupGet(db => db.InferenceProviders)
            .Returns(MockDbSetFactory.Create(providers ?? []));
        context.SetupGet(db => db.InferenceRoutes)
            .Returns(MockDbSetFactory.Create(routes ?? []));
        context.SetupGet(db => db.AppSettings)
            .Returns(MockDbSetFactory.Create(settings ?? []));
        context.Setup(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);
        return context;
    }

}
