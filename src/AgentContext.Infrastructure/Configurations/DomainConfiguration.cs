using AgentContext.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DomainEntity = AgentContext.Domain.Entities.Domain;

namespace AgentContext.Infrastructure.Configurations;

public sealed class DomainConfiguration : IEntityTypeConfiguration<DomainEntity>
{
    public void Configure(EntityTypeBuilder<DomainEntity> builder)
    {
        builder.ToTable("domains");
        builder.Property(d => d.Name).HasMaxLength(100).IsRequired();
        builder.HasIndex(d => new { d.WorkspaceId, d.Name }).IsUnique();
        builder.HasMany(d => d.Knowledge).WithOne(k => k.Domain).HasForeignKey(k => k.DomainId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(d => d.Skills).WithOne(s => s.Domain).HasForeignKey(s => s.DomainId).OnDelete(DeleteBehavior.Cascade);
    }
}
