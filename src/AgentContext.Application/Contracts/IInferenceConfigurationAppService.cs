using AgentContext.Application.Dtos;

namespace AgentContext.Application.Contracts;

/// <summary>
/// Reads, validates, and persists the platform-level inference configuration.
/// The same contract is used by Settings and the first-run Setup wizard.
/// </summary>
public interface IInferenceConfigurationAppService
{
    Task<InferenceConfigurationDto> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>Resolves protected credentials for the Learning Engine without exposing them over REST.</summary>
    Task<InferenceRuntimeOptions?> GetRuntimeOptionsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests both draft routes and persists only after both connection checks pass.
    /// </summary>
    Task<InferenceConfigurationDto> SaveAsync(
        InferenceConfigurationInput input,
        CancellationToken cancellationToken = default);

    /// <summary>Tests the unsaved draft without writing configuration, sessions, or usage.</summary>
    Task<InferenceValidationResult> VerifyAsync(
        InferenceConfigurationInput input,
        CancellationToken cancellationToken = default);
}
