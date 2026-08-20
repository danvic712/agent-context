using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Moq;
using DomainEntity = AgentContext.Domain.Entities.Domain;

namespace AgentContext.Application.Tests.TestSupport;

internal static class MockSkillDbContext
{
    public static Mock<AgentContextDbContext> Create(
        IEnumerable<Workspace>? workspaces = null,
        IEnumerable<DomainEntity>? domains = null,
        IEnumerable<Skill>? skills = null)
    {
        var options = new DbContextOptionsBuilder<AgentContextDbContext>().Options;
        var context = new Mock<AgentContextDbContext>(MockBehavior.Strict, options);
        context.SetupGet(db => db.Workspaces)
            .Returns(MockDbSetFactory.Create(workspaces ?? []));
        context.SetupGet(db => db.Domains)
            .Returns(MockDbSetFactory.Create(domains ?? []));
        context.SetupGet(db => db.Skills)
            .Returns(MockDbSetFactory.Create(skills ?? []));
        return context;
    }
}
