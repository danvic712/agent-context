using AgentContext.Application.Learning;
using AgentContext.Application.Localization;
using AgentContext.Application.Settings;
using AgentContext.Infrastructure;
using AgentContext.Tests.Testcontainers;
using Microsoft.EntityFrameworkCore;

namespace AgentContext.Tests.SeamTests;

/// <summary>
/// DB-backed platform settings (spec: "settings (LLM endpoint)"): the Learning
/// Engine's LLM endpoint configuration is stored in the <c>settings</c> table —
/// setter-uppable at runtime, not through app configuration.
/// </summary>
public sealed class SettingsAppServiceTests : PostgresTestBase
{
    private async Task<(SettingsAppService Service, AgentContextDbContext Db)> SeededAsync()
    {
        var db = Fixture.CreateDbContext();
        await db.Database.MigrateAsync();
        return (new SettingsAppService(db), db);
    }

    private static LlmOptions ValidOptions() => new()
    {
        BaseUrl = "http://localhost:11434/v1",
        ApiKey = "secret",
        Model = "llama3.2",
    };

    [Fact]
    public async Task Save_then_read_round_trips_the_llm_configuration()
    {
        var (service, db) = await SeededAsync();

        await service.SaveLlmOptionsAsync(new LlmOptions
        {
            BaseUrl = "https://api.openai.com/v1",
            ApiKey = "sk-test",
            Model = "gpt-4o-mini",
            EmbeddingModel = "text-embedding-3-small",
        });

        var loaded = await service.GetLlmOptionsAsync();

        Assert.NotNull(loaded);
        Assert.Equal("https://api.openai.com/v1", loaded!.BaseUrl);
        Assert.Equal("sk-test", loaded.ApiKey);
        Assert.Equal("gpt-4o-mini", loaded.Model);
        Assert.Equal("text-embedding-3-small", loaded.EmbeddingModel);
    }

    [Fact]
    public async Task Read_returns_null_when_the_endpoint_is_not_configured()
    {
        var (service, _) = await SeededAsync();

        Assert.Null(await service.GetLlmOptionsAsync());
    }

    [Fact]
    public async Task Read_returns_null_when_a_required_key_is_missing()
    {
        var (service, db) = await SeededAsync();
        await service.SaveLlmOptionsAsync(ValidOptions());

        // Simulate a partial/corrupt store (e.g. manual edit): drop the model key.
        await db.AppSettings.Where(s => s.Key == SettingKeys.LlmModel).ExecuteDeleteAsync();

        Assert.Null(await service.GetLlmOptionsAsync());
    }

    [Fact]
    public async Task Save_upserts_without_duplicates_and_rewrites_values()
    {
        var (service, db) = await SeededAsync();

        await service.SaveLlmOptionsAsync(ValidOptions());
        await service.SaveLlmOptionsAsync(new LlmOptions
        {
            BaseUrl = "http://localhost:11434/v1",
            ApiKey = "rotated-key",
            Model = "llama3.2",
        });

        var rows = await db.AppSettings.AsNoTracking().ToListAsync();
        Assert.Equal(3, rows.Count); // baseUrl, apiKey, model — embedding model omitted (== model)
        Assert.Single(rows, r => r.Key == SettingKeys.LlmApiKey && r.Value == "rotated-key");
    }

    [Fact]
    public async Task Save_rejects_invalid_configuration()
    {
        var (service, _) = await SeededAsync();

        var ex = await Assert.ThrowsAsync<LocalizedException>(() =>
            service.SaveLlmOptionsAsync(new LlmOptions { BaseUrl = "not a uri", ApiKey = "", Model = "" }));
        Assert.Equal(ErrorCodes.Llm.BaseUrlInvalid, ex.ErrorCode);
    }
}
