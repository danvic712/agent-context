namespace AgentContext.Application.Dtos;

/// <summary>
/// Complete staged draft for publishing a new immutable Skill version. The base
/// version is supplied by the URL; <c>previous_version_id</c> is always assigned
/// by the application service and is never accepted from this request.
/// </summary>
public sealed record PublishSkillVersionRequest(
    string Name,
    string Description,
    string Instructions,
    IReadOnlyList<SkillFileChange>? Files = null,
    IReadOnlyList<string>? Folders = null,
    IReadOnlyList<SkillPathRename>? Renames = null,
    IReadOnlyList<string>? DeletedPaths = null);
