namespace AgentContext.Application.Dtos;

/// <summary>
/// Input for creating a Skill (T6 / spec US21): markdown instructions + version + domain.
/// The slug is the stable identifier used by get_skill (skill://{domain}/{slug}).
/// </summary>
public sealed record CreateSkillRequest(
    string Domain,
    string Slug,
    string Name,
    string Description,
    string Instructions);
