using AgentContext.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using KnowledgeDomain = AgentContext.Domain.Entities.Domain;

namespace AgentContext.Infrastructure;

/// <summary>
/// EF Core schema for the confirmed MVP data model
/// (Workspace/Domain/User/Membership/Session/Knowledge/Skill/Usage).
/// Postgres + pgvector; the vector extension and HNSW index are created by migration.
/// </summary>
public sealed class AgentContextDbContext(DbContextOptions<AgentContextDbContext> options) : DbContext(options)
{
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<KnowledgeDomain> Domains => Set<KnowledgeDomain>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Knowledge> Knowledge => Set<Knowledge>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Usage> Usage => Set<Usage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        // ---- Workspace ------------------------------------------------------
        modelBuilder.Entity<Workspace>(b =>
        {
            b.ToTable("workspaces");
            b.Property(w => w.Name).HasMaxLength(200).IsRequired();
            b.Property(w => w.Type).HasConversion<string>().HasMaxLength(20);
            b.HasMany(w => w.Domains).WithOne(d => d.Workspace).HasForeignKey(d => d.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(w => w.Sessions).WithOne(s => s.Workspace).HasForeignKey(s => s.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Domain ---------------------------------------------------------
        modelBuilder.Entity<KnowledgeDomain>(b =>
        {
            b.ToTable("domains");
            b.Property(d => d.Name).HasMaxLength(100).IsRequired();
            b.HasIndex(d => new { d.WorkspaceId, d.Name }).IsUnique();
            b.HasMany(d => d.Knowledge).WithOne(k => k.Domain).HasForeignKey(k => k.DomainId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(d => d.Skills).WithOne(s => s.Domain).HasForeignKey(s => s.DomainId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---- User -----------------------------------------------------------
        modelBuilder.Entity<User>(b =>
        {
            b.ToTable("users");
            b.Property(u => u.DisplayName).HasMaxLength(200).IsRequired();
            b.Property(u => u.Email).HasMaxLength(320).IsRequired();
            b.HasIndex(u => u.Email).IsUnique();
            b.Property(u => u.PasswordHash).HasMaxLength(512).IsRequired();
        });

        // ---- Membership -----------------------------------------------------
        modelBuilder.Entity<Membership>(b =>
        {
            b.ToTable("memberships");
            b.Property(m => m.Role).HasConversion<string>().HasMaxLength(20);
            b.HasIndex(m => new { m.WorkspaceId, m.UserId }).IsUnique();
            b.HasOne(m => m.User).WithMany(u => u.Memberships).HasForeignKey(m => m.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Session --------------------------------------------------------
        modelBuilder.Entity<Session>(b =>
        {
            b.ToTable("sessions");
            b.Property(s => s.AgentName).HasMaxLength(100).IsRequired();
            b.Property(s => s.Task).IsRequired();
            b.Property(s => s.Conclusion).IsRequired();
            b.Property(s => s.SummaryJson).HasColumnType("jsonb").IsRequired();
            b.Property(s => s.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(s => s.FullContext).HasColumnType("text");
            b.Property(s => s.LastError).HasColumnType("text");
            // Queue access pattern for the Learning Engine worker (ADR 0005).
            b.HasIndex(s => new { s.Status, s.NextAttemptAtUtc });
            b.HasIndex(s => new { s.WorkspaceId, s.DomainId });
            b.HasOne(s => s.Domain).WithMany().HasForeignKey(s => s.DomainId).OnDelete(DeleteBehavior.SetNull);
            b.HasMany(s => s.Usage).WithOne(u => u.Session).HasForeignKey(u => u.SessionId).OnDelete(DeleteBehavior.Cascade);
            b.HasMany(s => s.Knowledge).WithOne(k => k.SourceSession).HasForeignKey(k => k.SourceSessionId).OnDelete(DeleteBehavior.SetNull);
        });

        // ---- Knowledge ------------------------------------------------------
        modelBuilder.Entity<Knowledge>(b =>
        {
            b.ToTable("knowledge");
            b.Property(k => k.Type).HasConversion<string>().HasMaxLength(20);
            b.Property(k => k.Title).HasMaxLength(500).IsRequired();
            b.Property(k => k.Content).HasColumnType("text").IsRequired();
            b.Property(k => k.Confidence).HasPrecision(5, 4);
            b.Property(k => k.Status).HasConversion<string>().HasMaxLength(20);
            b.Property(k => k.ConflictGroupId).HasMaxLength(64);
            b.HasIndex(k => new { k.DomainId, k.Status });
            b.HasIndex(k => new { k.WorkspaceId, k.SourceSessionId });
            b.HasIndex(k => k.ConflictGroupId);
            // pgvector HNSW index for semantic retrieval (search_memory / find_similar_solution).
            b.HasIndex(k => k.Embedding)
                .HasMethod("hnsw")
                .HasOperators("vector_l2_ops")
                .HasStorageParameter("m", 16)
                .HasStorageParameter("ef_construction", 64);
            b.HasOne(k => k.Workspace).WithMany().HasForeignKey(k => k.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        });

        // ---- Skill ----------------------------------------------------------
        modelBuilder.Entity<Skill>(b =>
        {
            b.ToTable("skills");
            b.Property(s => s.Slug).HasMaxLength(100).IsRequired();
            b.Property(s => s.Name).HasMaxLength(200).IsRequired();
            b.Property(s => s.Instructions).HasColumnType("text").IsRequired();
            b.HasIndex(s => new { s.WorkspaceId, s.DomainId, s.Slug }).IsUnique();
        });

        // ---- Usage ----------------------------------------------------------
        modelBuilder.Entity<Usage>(b =>
        {
            b.ToTable("usage");
            b.Property(u => u.Model).HasMaxLength(100).IsRequired();
            b.Property(u => u.Cost).HasPrecision(18, 6);
            b.HasIndex(u => new { u.SessionId, u.Model });
        });
    }
}
