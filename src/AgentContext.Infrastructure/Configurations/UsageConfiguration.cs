using AgentContext.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentContext.Infrastructure.Configurations;

public sealed class UsageConfiguration : IEntityTypeConfiguration<Usage>
{
    public void Configure(EntityTypeBuilder<Usage> builder)
    {
        builder.ToTable("usage");
        builder.Property(u => u.Model).HasMaxLength(100).IsRequired();
        builder.Property(u => u.Cost).HasPrecision(18, 6);
        builder.HasIndex(u => new { u.SessionId, u.Model });
    }
}
