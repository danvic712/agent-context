using AgentContext.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AgentContext.Infrastructure.Configurations;

public sealed class WorkspaceConfiguration : IEntityTypeConfiguration<Workspace>
{
    public void Configure(EntityTypeBuilder<Workspace> builder)
    {
        builder.ToTable("workspaces");
        builder.Property(w => w.Name).HasMaxLength(200).IsRequired();
        builder.Property(w => w.Type).HasConversion<string>().HasMaxLength(20);
        builder.HasMany(w => w.Domains).WithOne(d => d.Workspace).HasForeignKey(d => d.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(w => w.Sessions).WithOne(s => s.Workspace).HasForeignKey(s => s.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
    }
}
