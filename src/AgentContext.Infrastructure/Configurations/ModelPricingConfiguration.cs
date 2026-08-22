using AgentContext.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentContext.Infrastructure.Configurations;

public sealed class ModelPricingConfiguration : IEntityTypeConfiguration<ModelPricing>
{
    public void Configure(EntityTypeBuilder<ModelPricing> builder)
    {
        builder.ToTable("model_pricing");
        builder.Property(p => p.Model).HasMaxLength(200).IsRequired();
        // Per-token USD rates need far more precision than report-time aggregates.
        builder.Property(p => p.InputCostPerToken).HasPrecision(38, 18);
        builder.Property(p => p.OutputCostPerToken).HasPrecision(38, 18);
        builder.HasIndex(p => p.Model).IsUnique();
    }
}
