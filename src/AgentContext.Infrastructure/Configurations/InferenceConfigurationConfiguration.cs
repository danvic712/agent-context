using AgentContext.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentContext.Infrastructure.Configurations;

public sealed class InferenceConfigurationConfiguration : IEntityTypeConfiguration<InferenceConfiguration>
{
    public void Configure(EntityTypeBuilder<InferenceConfiguration> builder)
    {
        builder.ToTable("inference_configurations");
        builder.HasKey(configuration => configuration.Id).HasName("pk_inference_configurations");
        builder.Property(configuration => configuration.Id).HasColumnName("id");
        builder.Property(configuration => configuration.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(configuration => configuration.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(configuration => configuration.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
    }
}
