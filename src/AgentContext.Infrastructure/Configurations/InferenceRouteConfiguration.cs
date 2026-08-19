using AgentContext.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentContext.Infrastructure.Configurations;

public sealed class InferenceRouteConfiguration : IEntityTypeConfiguration<InferenceRoute>
{
    public void Configure(EntityTypeBuilder<InferenceRoute> builder)
    {
        builder.ToTable("inference_routes");
        builder.HasKey(route => route.Id).HasName("pk_inference_routes");
        builder.Property(route => route.Id).HasColumnName("id");
        builder.Property(route => route.InferenceConfigurationId).HasColumnName("inference_configuration_id").IsRequired();
        builder.Property(route => route.Capability).HasColumnName("capability").HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(route => route.ProviderId).HasColumnName("provider_id").IsRequired();
        builder.Property(route => route.Model).HasColumnName("model").HasMaxLength(200).IsRequired();

        builder.HasIndex(route => new { route.InferenceConfigurationId, route.Capability })
            .IsUnique()
            .HasDatabaseName("uq_inference_routes_inference_configuration_id_capability");

        builder.HasIndex(route => route.ProviderId)
            .HasDatabaseName("ix_inference_routes_provider_id");

        builder.HasOne(route => route.InferenceConfiguration)
            .WithMany(configuration => configuration.Routes)
            .HasForeignKey(route => route.InferenceConfigurationId)
            .OnDelete(DeleteBehavior.Cascade)
            .HasConstraintName("fk_inference_routes_inference_configurations_inference_configuration_id");

        builder.HasOne(route => route.Provider)
            .WithMany(provider => provider.Routes)
            .HasForeignKey(route => route.ProviderId)
            .OnDelete(DeleteBehavior.Restrict)
            .HasConstraintName("fk_inference_routes_inference_providers_provider_id");
    }
}
