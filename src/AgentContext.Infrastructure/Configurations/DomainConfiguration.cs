using AgentContext.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DomainEntity = AgentContext.Domain.Entities.Domain;

namespace AgentContext.Infrastructure.Configurations;

public sealed class DomainConfiguration : IEntityTypeConfiguration<DomainEntity>
{
    public void Configure(EntityTypeBuilder<DomainEntity> builder)
    {
        builder.ToTable("domains");
        builder.HasKey(d => d.Id).HasName("pk_domains");
        builder.Property(d => d.Id).HasColumnName("id");
        builder.Property(d => d.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(d => d.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(d => d.IsShared).HasColumnName("is_shared").IsRequired();
        builder.Property(d => d.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.HasIndex(d => new { d.WorkspaceId, d.Name })
            .IsUnique()
            .HasDatabaseName("uq_domains_workspace_id_name");
        builder.HasOne(d => d.Workspace)
            .WithMany(w => w.Domains)
            .HasForeignKey(d => d.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_domains_workspaces_workspace_id");
        builder.HasMany(d => d.Knowledge)
            .WithOne(k => k.Domain)
            .HasForeignKey(k => k.DomainId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_knowledge_domains_domain_id");
        builder.HasMany(d => d.Skills)
            .WithOne(s => s.Domain)
            .HasForeignKey(s => s.DomainId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_skills_domains_domain_id");
    }
}
