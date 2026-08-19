using AgentContext.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentContext.Infrastructure.Configurations;

public sealed class InferenceProviderConfiguration : IEntityTypeConfiguration<InferenceProvider>
{
    public void Configure(EntityTypeBuilder<InferenceProvider> builder)
    {
        builder.ToTable("inference_providers");
        builder.HasKey(provider => provider.Id).HasName("pk_inference_providers");
        builder.Property(provider => provider.Id).HasColumnName("id");
        builder.Property(provider => provider.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(provider => provider.ProviderType).HasColumnName("provider_type").HasMaxLength(64).IsRequired();
        builder.Property(provider => provider.BaseUrl).HasColumnName("base_url").HasMaxLength(2048).IsRequired();
        builder.Property(provider => provider.ApiKeySecretRef).HasColumnName("api_key_secret_ref").HasMaxLength(4096).IsRequired();
        builder.Property(provider => provider.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(provider => provider.UpdatedAtUtc).HasColumnName("updated_at_utc").IsRequired();
    }
}
