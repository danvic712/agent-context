using AgentContext.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentContext.Infrastructure.Configurations;

public sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable("memberships");
        builder.HasKey(m => m.Id).HasName("pk_memberships");
        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(m => m.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(m => m.Role)
            .HasColumnName("role")
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(m => m.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.HasIndex(m => m.UserId)
            .HasDatabaseName("ix_memberships_user_id");
        builder.HasIndex(m => new { m.WorkspaceId, m.UserId })
            .IsUnique()
            .HasDatabaseName("uq_memberships_workspace_id_user_id");
        builder.HasOne(m => m.User)
            .WithMany(u => u.Memberships)
            .HasForeignKey(m => m.UserId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_memberships_users_user_id");
        builder.HasOne(m => m.Workspace)
            .WithMany(w => w.Memberships)
            .HasForeignKey(m => m.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_memberships_workspaces_workspace_id");
    }
}
