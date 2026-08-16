using AgentContext.Tests.Testcontainers;
using Microsoft.EntityFrameworkCore;

namespace AgentContext.Tests.SeamTests;

/// <summary>
/// Primary seam — schema bootstrapping: EF Core migrations must create every MVP
/// entity against Postgres with pgvector, including the vector extension, the
/// embedding column, and the HNSW index (T1 acceptance criteria).
/// </summary>
public sealed class SchemaBootTests : PostgresTestBase
{
    [Fact]
    public async Task Migrations_create_all_mvp_entities_on_pgvector()
    {
        await using var db = Fixture.CreateDbContext();

        await db.Database.MigrateAsync();

        var tables = await db.Database
            .SqlQueryRaw<string>(
                "SELECT tablename AS \"Value\" FROM pg_tables WHERE schemaname = 'public' AND tablename NOT LIKE '\\_\\_%' ORDER BY tablename")
            .ToListAsync();

        Assert.Equal(
            ["domains", "knowledge", "memberships", "sessions", "settings", "skills", "usage", "users", "workspaces"],
            tables);

        var extension = await db.Database
            .SqlQueryRaw<string>("SELECT extname AS \"Value\" FROM pg_extension WHERE extname = 'vector'")
            .SingleOrDefaultAsync();
        Assert.Equal("vector", extension);

        var embeddingColumnType = await db.Database
            .SqlQueryRaw<string>(
                "SELECT format_type(atttypid, atttypmod) AS \"Value\" FROM pg_attribute WHERE attrelid = 'knowledge'::regclass AND attname = 'Embedding'")
            .SingleOrDefaultAsync();
        Assert.Equal("vector(1536)", embeddingColumnType);
    }

    [Fact]
    public async Task Knowledge_embedding_has_hnsw_index_for_semantic_retrieval()
    {
        await using var db = Fixture.CreateDbContext();

        await db.Database.MigrateAsync();

        var index = await db.Database
            .SqlQueryRaw<string>(
                "SELECT indexdef AS \"Value\" FROM pg_indexes WHERE tablename = 'knowledge' AND indexdef LIKE '%hnsw%'")
            .SingleOrDefaultAsync();

        Assert.NotNull(index);
        Assert.Contains("vector_l2_ops", index);
    }
}
