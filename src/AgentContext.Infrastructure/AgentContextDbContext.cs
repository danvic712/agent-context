using AgentContext.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using DomainEntity = AgentContext.Domain.Entities.Domain;

namespace AgentContext.Infrastructure;

/// <summary>
/// EF Core schema for the confirmed MVP data model
/// (Workspace/Domain/User/Membership/Session/Knowledge/Skill/Usage).
/// Postgres + pgvector; the vector extension and HNSW index are created by migration.
/// Entity mappings live in <c>AgentContext.Infrastructure.Configurations</c>
/// (fluent <c>IEntityTypeConfiguration&lt;T&gt;</c> classes, discovered by assembly scan).
/// </summary>
public sealed class AgentContextDbContext(DbContextOptions<AgentContextDbContext> options) : DbContext(options)
{
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<DomainEntity> Domains => Set<DomainEntity>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<Knowledge> Knowledge => Set<Knowledge>();
    public DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Usage> Usage => Set<Usage>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<ModelPricing> ModelPricings => Set<ModelPricing>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgentContextDbContext).Assembly);
    }
}
