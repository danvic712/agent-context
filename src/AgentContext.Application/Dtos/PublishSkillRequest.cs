namespace AgentContext.Application.Dtos;

/// <summary>
/// Input for publishing a new version of an existing Skill (T6 / spec US22):
/// the edited instructions are versioned on top of the current latest row;
/// the old version stays as history.
/// </summary>
public sealed record PublishSkillRequest(
    string Name,
    string Description,
    string Instructions);
