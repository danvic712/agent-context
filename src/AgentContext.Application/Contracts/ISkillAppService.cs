using AgentContext.Application.Dtos;

namespace AgentContext.Application.Contracts;

/// <summary>
/// Thin Skill management (T6 / spec US21–23): centrally-managed skills instead of
/// per-machine installation. CRUD over the latest version plus publish-new-version
/// (older versions are kept as history), and get_skill by (domain, slug) for agents.
/// </summary>
public interface ISkillAppService
{
    /// <summary>
    /// Creates a Skill in the given domain at version 1. Throws
    /// <see cref="ArgumentException"/> when the slug already exists in the domain
    /// (the slug is the stable identifier of a skill).
    /// </summary>
    Task<SkillDetail> CreateAsync(CreateSkillRequest request, CancellationToken cancellationToken = default);

    /// <summary>All Skills in the platform, newest version per (domain, slug) first.</summary>
    Task<IReadOnlyList<SkillListItem>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// get_skill: the latest published version of the skill at (domain, slug).
    /// Throws <see cref="KeyNotFoundException"/> when no such skill exists.
    /// </summary>
    Task<SkillDetail> GetBySlugAsync(string domain, string slug, CancellationToken cancellationToken = default);

    /// <summary>The Skill row with the given id (list rows reference the latest version).</summary>
    Task<SkillDetail> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Publishes a new version on top of the skill with the given id: the row is
    /// kept as history and a new row with Version+1 is inserted. The new version
    /// is created within the same domain and keeps the same slug.
    /// </summary>
    Task<SkillDetail> PublishAsync(Guid id, PublishSkillRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the skill (every version of its (domain, slug)) so get_skill can no
    /// longer resolve it. Throws <see cref="KeyNotFoundException"/> when the id is unknown.
    /// </summary>
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
