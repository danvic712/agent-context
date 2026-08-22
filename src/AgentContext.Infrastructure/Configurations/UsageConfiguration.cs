using AgentContext.Domain.Entities;
using AgentContext.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AgentContext.Infrastructure.Configurations;

public sealed class UsageConfiguration : IEntityTypeConfiguration<Usage>
{
    public void Configure(EntityTypeBuilder<Usage> builder)
    {
        builder.ToTable("usage", table =>
        {
            table.HasCheckConstraint(
                "ck_usage_source_valid",
                "source IN ('reported_session', 'learning_engine')");
            table.HasCheckConstraint(
                "ck_usage_tokens_non_negative",
                "input_tokens >= 0 AND cached_input_tokens >= 0 AND output_tokens >= 0");
            table.HasCheckConstraint(
                "ck_usage_cached_input_subset",
                "cached_input_tokens <= input_tokens");
            table.HasCheckConstraint(
                "ck_usage_capability_valid",
                "capability IS NULL OR capability IN ('Chat', 'Embedding')");
            table.HasCheckConstraint(
                "ck_usage_source_relationships",
                "(source = 'reported_session' AND session_id IS NOT NULL AND inference_route_id IS NULL AND capability IS NULL) OR " +
                "source = 'learning_engine'");
        });

        builder.HasKey(usage => usage.Id).HasName("pk_usage");
        builder.Property(usage => usage.Id).HasColumnName("id");
        builder.Property(usage => usage.SessionId).HasColumnName("session_id");
        builder.Property(usage => usage.Model).HasColumnName("model").HasMaxLength(200).IsRequired();
        builder.Property(usage => usage.InputTokens).HasColumnName("input_tokens").IsRequired();
        builder.Property(usage => usage.CachedInputTokens).HasColumnName("cached_input_tokens").IsRequired();
        builder.Property(usage => usage.OutputTokens).HasColumnName("output_tokens").IsRequired();
        builder.Property(usage => usage.Source)
            .HasColumnName("source")
            .HasMaxLength(32)
            .IsRequired()
            .HasConversion(new ValueConverter<UsageSource, string>(
                source => source == UsageSource.LearningEngine ? "learning_engine" : "reported_session",
                source => source == "learning_engine" ? UsageSource.LearningEngine : UsageSource.ReportedSession));
        builder.Property(usage => usage.InferenceRouteId).HasColumnName("inference_route_id");
        builder.Property(usage => usage.Capability)
            .HasColumnName("capability")
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(usage => usage.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

        builder.HasIndex(usage => new { usage.SessionId, usage.CreatedAtUtc })
            .HasDatabaseName("ix_usage_session_id_created_at_utc");
        builder.HasIndex(usage => new { usage.Source, usage.CreatedAtUtc })
            .HasDatabaseName("ix_usage_source_created_at_utc");
        builder.HasIndex(usage => usage.InferenceRouteId)
            .HasDatabaseName("ix_usage_inference_route_id");

        builder.HasOne(usage => usage.Session)
            .WithMany(session => session.Usage)
            .HasForeignKey(usage => usage.SessionId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_usage_sessions_session_id");

        // Route deletion/replacement must not delete historical usage; the
        // nullable binding is deliberately cleared by the database instead.
        builder.HasOne(usage => usage.InferenceRoute)
            .WithMany()
            .HasForeignKey(usage => usage.InferenceRouteId)
            .OnDelete(DeleteBehavior.SetNull)
            .HasConstraintName("fk_usage_inference_routes_inference_route_id");
    }
}
