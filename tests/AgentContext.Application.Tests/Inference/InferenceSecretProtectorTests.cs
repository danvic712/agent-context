using AgentContext.Application.Inference;
using Microsoft.AspNetCore.DataProtection;
using Moq;
using System.Text;

namespace AgentContext.Application.Tests.Inference;

public sealed class InferenceSecretProtectorTests
{
    [Fact]
    public void Unprotect_returns_empty_for_an_unconfigured_secret()
    {
        var protector = new Mock<IDataProtector>(MockBehavior.Strict);
        var provider = new Mock<IDataProtectionProvider>();
        provider
            .Setup(item => item.CreateProtector("agent-context/inference-provider-api-key/v1"))
            .Returns(protector.Object);
        var service = new InferenceSecretProtector(provider.Object);

        var result = service.Unprotect(string.Empty);

        Assert.Equal(string.Empty, result);
        protector.Verify(item => item.Unprotect(It.IsAny<byte[]>()), Times.Never);
    }

    [Fact]
    public void Unprotect_returns_the_plaintext_for_a_valid_protected_secret()
    {
        var protector = new Mock<IDataProtector>();
        var protectedValue = Convert.ToBase64String(Encoding.UTF8.GetBytes("protected"));
        protector
            .Setup(item => item.Unprotect(It.Is<byte[]>(value => Encoding.UTF8.GetString(value) == "protected")))
            .Returns(Encoding.UTF8.GetBytes("plain-text"));
        var provider = new Mock<IDataProtectionProvider>();
        provider
            .Setup(item => item.CreateProtector("agent-context/inference-provider-api-key/v1"))
            .Returns(protector.Object);
        var service = new InferenceSecretProtector(provider.Object);

        var result = service.Unprotect(protectedValue);

        Assert.Equal("plain-text", result);
    }

    [Fact]
    public void Unprotect_returns_null_when_the_protected_secret_cannot_be_read()
    {
        var protector = new Mock<IDataProtector>();
        var protectedValue = Convert.ToBase64String(Encoding.UTF8.GetBytes("invalid"));
        protector
            .Setup(item => item.Unprotect(It.Is<byte[]>(value => Encoding.UTF8.GetString(value) == "invalid")))
            .Throws<FormatException>();
        var provider = new Mock<IDataProtectionProvider>();
        provider
            .Setup(item => item.CreateProtector("agent-context/inference-provider-api-key/v1"))
            .Returns(protector.Object);
        var service = new InferenceSecretProtector(provider.Object);

        var result = service.Unprotect(protectedValue);

        Assert.Null(result);
    }
}
