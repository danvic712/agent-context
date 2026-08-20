using AgentContext.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentContext.Infrastructure.Configurations;

public sealed class SkillConfiguration : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("skills");

        builder.HasKey(s => s.Id).HasName("pk_skills");
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(s => s.DomainId).HasColumnName("domain_id").IsRequired();
        builder.Property(s => s.Slug).HasColumnName("slug").HasMaxLength(100).IsRequired();
        builder.Property(s => s.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(s => s.Description).HasColumnName("description").HasColumnType("text").IsRequired();
        builder.Property(s => s.Instructions).HasColumnName("instructions").HasColumnType("text").IsRequired();
        builder.Property(s => s.Version).HasColumnName("version").IsRequired();
        builder.Property(s => s.PreviousVersionId).HasColumnName("previous_version_id");
        builder.Property(s => s.SourceType).HasColumnName("source_type").HasMaxLength(32);
        builder.Property(s => s.SourceUrl).HasColumnName("source_url").HasColumnType("text");
        builder.Property(s => s.SourceRevision).HasColumnName("source_revision").HasMaxLength(256);
        builder.Property(s => s.SourceDigest).HasColumnName("source_digest").HasMaxLength(128);
        builder.Property(s => s.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(s => s.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();

        builder.HasOne(s => s.Workspace)
            .WithMany()
            .HasForeignKey(s => s.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_skills_workspaces_workspace_id");

        builder.HasOne(s => s.PreviousVersion)
            .WithMany(s => s.NextVersions)
            .HasForeignKey(s => s.PreviousVersionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_skills_previous_version_id");

        // T6 version history: one row per version, so a (workspace, domain, slug)
        // has a row for every published version. Version is monotonically
        // increasing per (domain, slug); get_skill returns the latest.
        builder.HasIndex(s => new { s.WorkspaceId, s.DomainId, s.Slug, s.Version })
            .IsUnique()
            .HasDatabaseName("uq_skills_workspace_id_domain_id_slug_version");
        builder.HasIndex(s => new { s.WorkspaceId, s.DomainId, s.Slug })
            .HasDatabaseName("ix_skills_workspace_id_domain_id_slug");
        builder.HasIndex(s => s.DomainId)
            .HasDatabaseName("ix_skills_domain_id");
        builder.HasIndex(s => s.PreviousVersionId)
            .HasDatabaseName("ix_skills_previous_version_id");

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("ck_skills_version_positive", "version >= 1");
            table.HasCheckConstraint(
                "ck_skills_source_type",
                "source_type IS NULL OR source_type IN ('manual', 'zip', 'skills_sh', 'local_copy')");
        });
    }
}
