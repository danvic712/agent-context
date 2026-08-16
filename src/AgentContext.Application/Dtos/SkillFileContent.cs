namespace AgentContext.Application.Dtos;

/// <summary>
/// A Skill package file with its content (T12): text files carry the content
/// directly; binary files carry a base64-encoded payload with <c>Binary = true</c>
/// (clients decode it).
/// </summary>
public sealed record SkillFileContent(string Path, string Content, bool Binary);
