namespace AgentContext.Application.Contracts;

/// <summary>Protects provider API keys before they are stored in PostgreSQL.</summary>
public interface IInferenceSecretProtector
{
    string Protect(string secret);

    string? Unprotect(string protectedSecret);
}
