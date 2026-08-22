using AgentContext.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using DomainEntity = AgentContext.Domain.Entities.Domain;

namespace AgentContext.Infrastructure.Configurations;

public sealed class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions");
        builder.Property(s => s.AgentName).HasMaxLength(100).IsRequired();
        builder.Property(s => s.Task).IsRequired();
        builder.Property(s => s.Conclusion).IsRequired();
        builder.Property(s => s.SummaryJson).HasColumnType("jsonb").IsRequired();
        builder.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
        builder.Property(s => s.FullContext).HasColumnType("text");
        builder.Property(s => s.LastError).HasColumnType("text");
        // Queue access pattern for the Learning Engine worker (ADR 0005).
        builder.HasIndex(s => new { s.Status, s.NextAttemptAtUtc });
        builder.HasIndex(s => new { s.WorkspaceId, s.DomainId });
        builder.HasOne(s => s.Domain).WithMany().HasForeignKey(s => s.DomainId).OnDelete(DeleteBehavior.SetNull);
        builder.HasMany(s => s.Knowledge).WithOne(k => k.SourceSession).HasForeignKey(k => k.SourceSessionId).OnDelete(DeleteBehavior.SetNull);
    }
}
