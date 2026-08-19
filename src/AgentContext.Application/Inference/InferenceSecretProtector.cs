using AgentContext.Application.Contracts;
using Microsoft.AspNetCore.DataProtection;

namespace AgentContext.Application.Inference;

/// <summary>ASP.NET Core Data Protection-backed provider key storage.</summary>
public sealed class InferenceSecretProtector(IDataProtectionProvider provider) : IInferenceSecretProtector
{
    private readonly IDataProtector protector = provider.CreateProtector("agent-context/inference-provider-api-key/v1");

    public string Protect(string secret) => protector.Protect(secret);

    public string? Unprotect(string protectedSecret)
    {
        try
        {
            return protector.Unprotect(protectedSecret);
        }
        catch (Exception) when (protectedSecret.Length > 0)
        {
            return null;
        }
    }
}
