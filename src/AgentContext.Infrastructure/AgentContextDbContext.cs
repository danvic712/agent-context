using AgentContext.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using DomainEntity = AgentContext.Domain.Entities.Domain;

namespace AgentContext.Infrastructure;

/// <summary>
/// EF Core schema for the confirmed MVP data model
/// (Workspace/Domain/User/Membership/Session/Knowledge/Skill/Usage/Inference).
/// Postgres + pgvector; the vector extension and HNSW index are created by migration.
/// Entity mappings live in <c>AgentContext.Infrastructure.Configurations</c>
/// (fluent <c>IEntityTypeConfiguration&lt;T&gt;</c> classes, discovered by assembly scan).
/// </summary>
public class AgentContextDbContext(DbContextOptions<AgentContextDbContext> options) : DbContext(options)
{
    public virtual DbSet<Workspace> Workspaces => Set<Workspace>();
    public virtual DbSet<DomainEntity> Domains => Set<DomainEntity>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<Session> Sessions => Set<Session>();
    public virtual DbSet<Knowledge> Knowledge => Set<Knowledge>();
    public virtual DbSet<Skill> Skills => Set<Skill>();
    public DbSet<Usage> Usage => Set<Usage>();
    public virtual DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<ModelPricing> ModelPricings => Set<ModelPricing>();
    public virtual DbSet<InferenceConfiguration> InferenceConfigurations => Set<InferenceConfiguration>();
    public virtual DbSet<InferenceRoute> InferenceRoutes => Set<InferenceRoute>();
    public virtual DbSet<InferenceProvider> InferenceProviders => Set<InferenceProvider>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AgentContextDbContext).Assembly);
    }
}
