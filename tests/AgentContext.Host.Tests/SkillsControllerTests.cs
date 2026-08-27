using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Host.Controllers;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace AgentContext.Host.Tests;

public sealed class SkillsControllerTests
{
    [Fact]
    public async Task Delete_removes_the_skill_and_returns_no_content()
    {
        var skillId = Guid.NewGuid();
        var skills = new Mock<ISkillAppService>(MockBehavior.Strict);
        skills.Setup(service => service.DeleteAsync(skillId, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var controller = new SkillsController(skills.Object);

        var result = await controller.Delete(skillId, CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
        skills.Verify(service => service.DeleteAsync(skillId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Download_returns_the_package_as_a_zip_attachment()
    {
        var skillId = Guid.NewGuid();
        var content = new MemoryStream([1, 2, 3]);
        var skills = new Mock<ISkillAppService>(MockBehavior.Strict);
        skills.Setup(service => service.DownloadPackageAsync(skillId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SkillPackageDownload(content, "uploaded-guide-v1.zip"));

        var controller = new SkillsController(skills.Object);

        var result = await controller.Download(skillId, CancellationToken.None);

        var file = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("application/zip", file.ContentType);
        Assert.Equal("uploaded-guide-v1.zip", file.FileDownloadName);
        Assert.Same(content, file.FileStream);
    }
}
