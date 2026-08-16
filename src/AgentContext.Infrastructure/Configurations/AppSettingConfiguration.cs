using AgentContext.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentContext.Infrastructure.Configurations;

public sealed class AppSettingConfiguration : IEntityTypeConfiguration<AppSetting>
{
    public void Configure(EntityTypeBuilder<AppSetting> builder)
    {
        builder.ToTable("settings");
        builder.HasKey(s => s.Key);
        builder.Property(s => s.Key).HasMaxLength(64);
        builder.Property(s => s.Value).HasMaxLength(1024).IsRequired();
    }
}
