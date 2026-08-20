using AgentContext.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentContext.Infrastructure.Configurations;

public sealed class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> builder)
    {
        builder.ToTable("settings");
        builder.HasKey(s => s.Key).HasName("pk_settings");
        builder.Property(s => s.Key)
            .HasColumnName("key")
            .HasMaxLength(64)
            .IsRequired();
        builder.Property(s => s.Value)
            .HasColumnName("value")
            .HasMaxLength(1024)
            .IsRequired();
    }
}
