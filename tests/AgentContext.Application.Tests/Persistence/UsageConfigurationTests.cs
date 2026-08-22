using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Pgvector.EntityFrameworkCore;

namespace AgentContext.Application.Tests.Persistence;

public sealed class UsageConfigurationTests
{
    [Fact]
    public void Usage_model_keeps_token_and_source_invariants_in_the_database_contract()
    {
        var options = new DbContextOptionsBuilder<AgentContextDbContext>()
            .UseNpgsql("Host=unused;Database=unused", npgsql => npgsql.UseVector())
            .Options;
        using var db = new AgentContextDbContext(options);

        var usage = db.GetService<IDesignTimeModel>().Model.FindEntityType(typeof(Usage));
        Assert.NotNull(usage);
        Assert.Equal("usage", usage!.GetTableName());

        var constraints = usage.GetCheckConstraints()
            .Where(constraint => constraint.Name is not null && constraint.Sql is not null)
            .ToDictionary(constraint => constraint.Name!, constraint => constraint.Sql!);
        Assert.Contains("input_tokens >= 0", constraints["ck_usage_tokens_non_negative"]);
        Assert.Equal("cached_input_tokens <= input_tokens", constraints["ck_usage_cached_input_subset"]);
        Assert.Contains("source = 'reported_session'", constraints["ck_usage_source_relationships"]);
    }
}
