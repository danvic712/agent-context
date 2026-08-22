using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Moq;
using KnowledgeEntity = AgentContext.Domain.Entities.Knowledge;

namespace AgentContext.Application.Tests.TestSupport;

internal static class MockKnowledgeDbContext
{
    public static Mock<AgentContextDbContext> Create(IEnumerable<KnowledgeEntity>? knowledge = null)
    {
        var options = new DbContextOptionsBuilder<AgentContextDbContext>().Options;
        var context = new Mock<AgentContextDbContext>(MockBehavior.Strict, options);
        context.SetupGet(db => db.Knowledge)
            .Returns(MockDbSetFactory.Create(knowledge ?? []));
        context.Setup(db => db.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(0);
        return context;
    }
}
