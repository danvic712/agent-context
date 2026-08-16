using AgentContext.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentContext.Infrastructure.Configurations;

public sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("skills");
        builder.Property(s => s.Slug).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Name).HasMaxLength(200).IsRequired();
        builder.Property(s => s.Instructions).HasColumnType("text").IsRequired();
        // T6 version history: one row per version, so a (workspace, domain, slug)
        // has a row for every published version. Version is monotonically
        // increasing per (domain, slug); get_skill returns the latest.
        builder.HasIndex(s => new { s.WorkspaceId, s.DomainId, s.Slug, s.Version }).IsUnique();
        builder.HasIndex(s => new { s.WorkspaceId, s.DomainId, s.Slug });
    }
}
