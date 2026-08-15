using AgentContext.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentContext.Infrastructure.Configurations;

public sealed class KnowledgeConfiguration : IEntityTypeConfiguration<Knowledge>
{
    public void Configure(EntityTypeBuilder<Knowledge> builder)
    {
        builder.ToTable("knowledge");
        builder.Property(k => k.Type).HasConversion<string>().HasMaxLength(20);
        builder.Property(k => k.Title).HasMaxLength(500).IsRequired();
        builder.Property(k => k.Content).HasColumnType("text").IsRequired();
        builder.Property(k => k.Confidence).HasPrecision(5, 4);
        builder.Property(k => k.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(k => k.ConflictGroupId).HasMaxLength(64);
        // Dimension 1536 matches the default OpenAI embedding model; migrate if
        // the configured endpoint requires another size.
        builder.Property(k => k.Embedding).HasColumnType("vector(1536)");
        builder.HasIndex(k => new { k.DomainId, k.Status });
        builder.HasIndex(k => new { k.WorkspaceId, k.SourceSessionId });
        builder.HasIndex(k => k.ConflictGroupId);
        // pgvector HNSW index for semantic retrieval (search_memory / find_similar_solution).
        builder.HasIndex(k => k.Embedding)
            .HasMethod("hnsw")
            .HasOperators("vector_l2_ops")
            .HasStorageParameter("m", 16)
            .HasStorageParameter("ef_construction", 64);
        builder.HasOne(k => k.Workspace).WithMany().HasForeignKey(k => k.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
    }
}
