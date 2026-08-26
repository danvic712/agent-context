using AgentContext.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DomainEntity = AgentContext.Domain.Entities.Domain;

namespace AgentContext.Infrastructure.Configurations;

public sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions");
        builder.HasKey(s => s.Id).HasName("pk_sessions");
        builder.Property(s => s.Id).HasColumnName("id");
        builder.Property(s => s.WorkspaceId).HasColumnName("workspace_id").IsRequired();
        builder.Property(s => s.DomainId).HasColumnName("domain_id");
        builder.Property(s => s.AgentName).HasColumnName("agent_name").HasMaxLength(100).IsRequired();
        builder.Property(s => s.Task).HasColumnName("task").IsRequired();
        builder.Property(s => s.Conclusion).HasColumnName("conclusion").IsRequired();
        builder.Property(s => s.SummaryJson).HasColumnName("summary_json").HasColumnType("jsonb").IsRequired();
        builder.Property(s => s.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(s => s.Remembered).HasColumnName("remembered").IsRequired();
        builder.Property(s => s.FullContext).HasColumnName("full_context").HasColumnType("text");
        builder.Property(s => s.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(s => s.ProcessedAtUtc).HasColumnName("processed_at_utc");
        builder.Property(s => s.NextAttemptAtUtc).HasColumnName("next_attempt_at_utc");
        builder.Property(s => s.ErrorCount).HasColumnName("error_count").IsRequired();
        builder.Property(s => s.LastError).HasColumnName("last_error").HasColumnType("text");
        // Queue access pattern for the Learning Engine worker (ADR 0005).
        builder.HasIndex(s => s.DomainId)
            .HasDatabaseName("ix_sessions_domain_id");
        builder.HasIndex(s => new { s.Status, s.NextAttemptAtUtc })
            .HasDatabaseName("ix_sessions_status_next_attempt_at_utc");
        builder.HasIndex(s => new { s.WorkspaceId, s.DomainId })
            .HasDatabaseName("ix_sessions_workspace_id_domain_id");
        builder.HasOne(s => s.Workspace)
            .WithMany(w => w.Sessions)
            .HasForeignKey(s => s.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_sessions_workspaces_workspace_id");
        builder.HasOne(s => s.Domain)
            .WithMany()
            .HasForeignKey(s => s.DomainId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_sessions_domains_domain_id");
        builder.HasMany(s => s.Knowledge)
            .WithOne(k => k.SourceSession)
            .HasForeignKey(k => k.SourceSessionId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_knowledge_sessions_source_session_id");
    }
}
