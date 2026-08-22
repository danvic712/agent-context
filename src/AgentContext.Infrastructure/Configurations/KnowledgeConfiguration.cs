using AgentContext.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentContext.Infrastructure.Configurations;

public sealed class KnowledgeConfiguration : IEntityTypeConfiguration<Knowledge>
{
    public void Configure(EntityTypeBuilder<Knowledge> builder)
    {
        builder.ToTable("knowledge");
        builder.HasKey(k => k.Id).HasName("pk_knowledge");
        builder.Property(k => k.Id).HasColumnName("id");
        builder.Property(k => k.WorkspaceId).HasColumnName("workspace_id");
        builder.Property(k => k.DomainId).HasColumnName("domain_id");
        builder.Property(k => k.Type).HasColumnName("type").HasConversion<string>().HasMaxLength(20);
        builder.Property(k => k.Title).HasColumnName("title").HasMaxLength(500).IsRequired();
        builder.Property(k => k.Content).HasColumnName("content").HasColumnType("text").IsRequired();
        builder.Property(k => k.Confidence).HasColumnName("confidence").HasPrecision(5, 4);
        builder.Property(k => k.IsPrivate).HasColumnName("is_private");
        builder.Property(k => k.SourceSessionId).HasColumnName("source_session_id");
        builder.Property(k => k.ConflictGroupId).HasColumnName("conflict_group_id").HasMaxLength(64);
        builder.Property(k => k.Embedding).HasColumnName("embedding").HasColumnType("vector(1536)");
        builder.Property(k => k.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20);
        builder.Property(k => k.CreatedAtUtc).HasColumnName("created_at_utc");
        builder.Property(k => k.UpdatedAtUtc).HasColumnName("updated_at_utc");
        builder.Property(k => k.LastUsedAtUtc).HasColumnName("last_used_at_utc");
        builder.Property(k => k.LastConfidenceDecayAtUtc).HasColumnName("last_confidence_decay_at_utc");

        builder.HasIndex(k => new { k.DomainId, k.Status })
            .HasDatabaseName("ix_knowledge_domain_id_status");
        builder.HasIndex(k => new { k.WorkspaceId, k.SourceSessionId })
            .HasDatabaseName("ix_knowledge_workspace_id_source_session_id");
        builder.HasIndex(k => k.SourceSessionId)
            .HasDatabaseName("ix_knowledge_source_session_id");
        builder.HasIndex(k => k.ConflictGroupId)
            .HasDatabaseName("ix_knowledge_conflict_group_id");
        builder.HasIndex(k => new { k.Status, k.CreatedAtUtc, k.Id })
            .HasDatabaseName("ix_knowledge_status_created_at_utc_id");
        builder.HasIndex(k => new { k.Status, k.Confidence, k.UpdatedAtUtc, k.Id })
            .HasDatabaseName("ix_knowledge_status_confidence_updated_at_utc_id");
        builder.HasIndex(k => new { k.Status, k.UpdatedAtUtc, k.Id })
            .HasDatabaseName("ix_knowledge_status_updated_at_utc_id");
        // pgvector HNSW index for semantic retrieval (search_memory / find_similar_solution).
        builder.HasIndex(k => k.Embedding)
            .HasDatabaseName("ix_knowledge_embedding_hnsw")
            .HasMethod("hnsw")
            .HasOperators("vector_l2_ops")
            .HasStorageParameter("m", 16)
            .HasStorageParameter("ef_construction", 64);
        builder.HasOne(k => k.Workspace)
            .WithMany()
            .HasForeignKey(k => k.WorkspaceId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_knowledge_workspaces_workspace_id");
    }
}
