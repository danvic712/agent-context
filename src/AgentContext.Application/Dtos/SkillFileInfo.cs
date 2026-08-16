namespace AgentContext.Application.Dtos;

/// <summary>
/// A file inside a Skill package (T12): path relative to the package root
/// (e.g. <c>SKILL.md</c> or <c>examples/trace.ts</c>), byte size and whether the
/// content is binary (contains NUL bytes).
/// </summary>
public sealed record SkillFileInfo(string Path, long Size, bool Binary);
