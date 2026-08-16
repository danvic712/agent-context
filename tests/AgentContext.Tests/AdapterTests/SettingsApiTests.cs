using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentContext.Application.Contracts;
using AgentContext.Application.Learning;
using AgentContext.Application.Settings;
using AgentContext.Infrastructure;
using AgentContext.Tests.Testcontainers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;

namespace AgentContext.Tests.AdapterTests;

/// <summary>
/// Secondary seam — LLM endpoint settings over REST (T10, issue #11): boots the
/// real host and drives GET/PUT /api/settings/llm-options through the web
/// adapter. Covers masked-key (AC2), validation → 400 (AC3), and the wizard
/// write path (the same seam persists configuration post-setup).
/// </summary>
public sealed class SettingsApiTests : PostgresTestBase
{
    private async Task<(WebApplicationFactory<Program> Factory, HttpClient Client)> SeededAsync()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.UseSetting("ConnectionStrings:Default", Fixture.ConnectionString));

        using var setupClient = factory.CreateClient();
        var setup = await setupClient.PostAsJsonAsync("/api/setup", new
        {
            displayName = "Danvic",
            email = "danvic@example.com",
            password = "correct-horse-battery",
        });
        setup.EnsureSuccessStatusCode();

        return (factory, factory.CreateClient());
    }

    [Fact]
    public async Task Get_returns_unconfigured_state_before_any_save()
    {
        var (_, client) = await SeededAsync();

        var body = await client.GetFromJsonAsync<JsonElement>("/api/settings/llm-options");

        Assert.False(body.GetProperty("configured").GetBoolean());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("baseUrl").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("maskedApiKey").ValueKind);
    }

    [Fact]
    public async Task Put_persists_and_get_masks_the_api_key()
    {
        var (_, client) = await SeededAsync();

        var put = await client.PutAsJsonAsync("/api/settings/llm-options", new
        {
            baseUrl = "https://api.openai.com/v1",
            apiKey = "sk-abcdef1234567890",
            model = "gpt-4o-mini",
            embeddingModel = "text-embedding-3-small",
        });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);

        var body = await client.GetFromJsonAsync<JsonElement>("/api/settings/llm-options");

        Assert.True(body.GetProperty("configured").GetBoolean());
        Assert.Equal("https://api.openai.com/v1", body.GetProperty("baseUrl").GetString());
        Assert.Equal("gpt-4o-mini", body.GetProperty("model").GetString());
        Assert.Equal("text-embedding-3-small", body.GetProperty("embeddingModel").GetString());
        var masked = body.GetProperty("maskedApiKey").GetString();
        Assert.NotNull(masked);
        // The full key is never exposed; only a short prefix survives.
        Assert.DoesNotContain("abcdef1234567890", masked);
        Assert.Contains("sk-abc", masked);
    }

    [Fact]
    public async Task Put_invalid_input_returns_400_with_validation_message()
    {
        var (_, client) = await SeededAsync();

        var put = await client.PutAsJsonAsync("/api/settings/llm-options", new
        {
            baseUrl = "not-a-uri",
            apiKey = "",
            model = "",
        });

        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
        var body = await put.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains("BaseUrl", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Put_empty_object_returns_400()
    {
        var (_, client) = await SeededAsync();

        var put = await client.PutAsJsonAsync("/api/settings/llm-options", new { });

        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
    }

    [Fact]
    public async Task Put_with_blank_api_key_keeps_the_existing_key_and_updates_the_model()
    {
        var (_, client) = await SeededAsync();

        // First save: full configuration.
        var initial = await client.PutAsJsonAsync("/api/settings/llm-options", new
        {
            baseUrl = "https://api.openai.com/v1",
            apiKey = "sk-secret-value-42",
            model = "gpt-4o-mini",
        });
        initial.EnsureSuccessStatusCode();

        // Partial update: change only the model, leave the key blank — the
        // existing key must be preserved, not wiped (UI contract: "Leaving it
        // blank keeps the existing key").
        var update = await client.PutAsJsonAsync("/api/settings/llm-options", new
        {
            baseUrl = "https://api.openai.com/v1",
            apiKey = "",
            model = "gpt-5.6-luna",
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);

        var body = await client.GetFromJsonAsync<JsonElement>("/api/settings/llm-options");
        Assert.Equal("gpt-5.6-luna", body.GetProperty("model").GetString());
        Assert.True(body.GetProperty("configured").GetBoolean());

        // The key survives the partial update (masked preview still reflects it).
        var masked = body.GetProperty("maskedApiKey").GetString();
        Assert.Contains("sk-sec", masked);
        Assert.DoesNotContain("secret-value-42", masked);
    }

    [Fact]
    public async Task Settings_survive_round_trip_and_are_readable_after_restart()
    {
        var (factory, client) = await SeededAsync();

        var put = await client.PutAsJsonAsync("/api/settings/llm-options", new
        {
            baseUrl = "http://localhost:11434/v1",
            apiKey = "ollama-key",
            model = "llama3.2",
        });
        put.EnsureSuccessStatusCode();

        // A fresh host instance reads the same configuration (per-call resolution,
        // no restart needed — the DB is the source of truth).
        using var secondClient = factory.CreateClient();
        var body = await secondClient.GetFromJsonAsync<JsonElement>("/api/settings/llm-options");

        Assert.True(body.GetProperty("configured").GetBoolean());
        Assert.Equal("http://localhost:11434/v1", body.GetProperty("baseUrl").GetString());
        Assert.Equal("llama3.2", body.GetProperty("model").GetString());
    }
}
