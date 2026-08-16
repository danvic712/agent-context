using AgentContext.Application.Dtos;
using AgentContext.Application.Pricing;
using AgentContext.Infrastructure;
using AgentContext.Tests.Testcontainers;
using Microsoft.EntityFrameworkCore;

namespace AgentContext.Tests.SeamTests;

/// <summary>
/// The maintained model pricing table (T7, issue #8 / spec US28): upsert by model,
/// list, delete. Primary seam against Testcontainers pgvector.
/// </summary>
public sealed class PricingTests : PostgresTestBase
{
    private async Task<AgentContextDbContext> MigratedDbAsync()
    {
        var db = Fixture.CreateDbContext();
        await db.Database.MigrateAsync();
        return db;
    }

    private static PricingAppService Service(AgentContextDbContext db) => new(db);

    [Fact]
    public async Task Save_creates_a_new_pricing_row()
    {
        await using var db = await MigratedDbAsync();

        var saved = await Service(db).SaveAsync(new SaveModelPricingRequest("gpt-4o", 0.0000025m, 0.00001m));

        Assert.Equal("gpt-4o", saved.Model);
        Assert.Equal(0.0000025m, saved.InputCostPerToken);
        Assert.Equal(0.00001m, saved.OutputCostPerToken);

        var row = await db.ModelPricings.AsNoTracking().SingleAsync();
        Assert.Equal("gpt-4o", row.Model);
    }

    [Fact]
    public async Task Save_upserts_by_model_name()
    {
        await using var db = await MigratedDbAsync();
        var service = Service(db);
        await service.SaveAsync(new SaveModelPricingRequest("gpt-4o", 0.0000025m, 0.00001m));

        var updated = await service.SaveAsync(new SaveModelPricingRequest("gpt-4o", 0.000003m, 0.000012m));

        Assert.Equal(1, await db.ModelPricings.CountAsync());
        Assert.Equal(0.000003m, updated.InputCostPerToken);
        Assert.Equal(0.000012m, updated.OutputCostPerToken);
    }

    [Fact]
    public async Task Save_rejects_empty_model_and_negative_rates()
    {
        await using var db = await MigratedDbAsync();
        var service = Service(db);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(new SaveModelPricingRequest(" ", 1m, 1m)));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(new SaveModelPricingRequest("gpt-4o", -1m, 1m)));
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(new SaveModelPricingRequest("gpt-4o", 1m, -1m)));
    }

    [Fact]
    public async Task List_returns_rows_ordered_by_model()
    {
        await using var db = await MigratedDbAsync();
        var service = Service(db);
        await service.SaveAsync(new SaveModelPricingRequest("z-model", 1m, 1m));
        await service.SaveAsync(new SaveModelPricingRequest("a-model", 2m, 3m));

        var rows = await service.ListAsync();

        Assert.Equal(["a-model", "z-model"], rows.Select(r => r.Model).ToArray());
        Assert.Equal(2, rows[0].InputCostPerToken);
        Assert.Equal(3, rows[0].OutputCostPerToken);
    }

    [Fact]
    public async Task Delete_removes_the_model_row()
    {
        await using var db = await MigratedDbAsync();
        var service = Service(db);
        await service.SaveAsync(new SaveModelPricingRequest("gpt-4o", 1m, 1m));

        await service.DeleteAsync("gpt-4o");

        Assert.Equal(0, await db.ModelPricings.CountAsync());
    }

    [Fact]
    public async Task Delete_unknown_model_is_a_no_op()
    {
        await using var db = await MigratedDbAsync();

        await Service(db).DeleteAsync("never-existed");

        Assert.Equal(0, await db.ModelPricings.CountAsync());
    }
}
