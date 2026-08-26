using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Application.Setup;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using AgentContext.Application.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;

namespace AgentContext.Application.Tests.Setup;

public sealed class SetupAppServiceTests
{
    [Fact]
    public async Task Configure_defers_an_untouched_inference_draft()
    {
        var context = CreateContext();
        var inference = new Mock<IInferenceConfigurationAppService>(MockBehavior.Strict);
        var transaction = new Mock<IDbContextTransaction>();
        var database = new Mock<DatabaseFacade>(context.Object);
        database
            .Setup(item => item.BeginTransactionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(transaction.Object);
        context.SetupGet(item => item.Database).Returns(database.Object);

        var service = new SetupAppService(context.Object, inference.Object);
        var request = new SetupRequest(
            "Ada Lovelace",
            "ada@example.com",
            "correct horse battery staple",
            "en-US",
            new InferenceConfigurationInput(
                "Platform default",
                [new InferenceProviderInput(Guid.NewGuid(), "OpenAI", "openai-compatible", "https://api.openai.com/v1", "")],
                [
                    new InferenceRouteInput(Guid.NewGuid(), InferenceCapability.Chat, Guid.NewGuid(), ""),
                    new InferenceRouteInput(Guid.NewGuid(), InferenceCapability.Embedding, Guid.NewGuid(), ""),
                ]));

        var result = await service.ConfigureAsync(request);

        Assert.NotEqual(Guid.Empty, result.UserId);
        inference.Verify(
            item => item.SaveAsync(It.IsAny<InferenceConfigurationInput>(), It.IsAny<CancellationToken>()),
            Times.Never);
        transaction.Verify(item => item.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    private static Mock<AgentContextDbContext> CreateContext()
    {
        var options = new DbContextOptionsBuilder<AgentContextDbContext>().Options;
        var context = new Mock<AgentContextDbContext>(MockBehavior.Strict, options);
        context.SetupGet(item => item.Users).Returns(MockDbSetFactory.Create<User>([]));
        context.SetupGet(item => item.Workspaces).Returns(MockDbSetFactory.Create<Workspace>([]));
        context.SetupGet(item => item.Memberships).Returns(MockDbSetFactory.Create<Membership>([]));
        context.SetupGet(item => item.AppSettings).Returns(MockDbSetFactory.Create<AppSetting>([]));
        context.Setup(item => item.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        return context;
    }
}
