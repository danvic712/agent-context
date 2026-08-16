using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AgentContext.Tests.Testcontainers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AgentContext.Tests.AdapterTests;

/// <summary>
/// Secondary seam — platform language over REST (T11, issue #12): drives
/// GET/PUT /api/settings/language through the real host. Covers the fallback to
/// en-US (AC1), the round-trip, the invalid-locale → 400 coded error, and the
/// error surface localizing to the configured language (AC4).
/// </summary>
public sealed class SettingsLanguageApiTests : PostgresTestBase
{
    private (WebApplicationFactory<Program> Factory, HttpClient Client) Seeded()
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
                builder.UseSetting("ConnectionStrings:Default", Fixture.ConnectionString));

        return (factory, factory.CreateClient());
    }

    [Fact]
    public async Task Get_falls_back_to_en_US_before_any_save()
    {
        var (_, client) = Seeded();

        var body = await client.GetFromJsonAsync<JsonElement>("/api/settings/language");

        Assert.Equal("en-US", body.GetProperty("language").GetString());
    }

    [Fact]
    public async Task Put_round_trips_the_language_and_normalizes_case()
    {
        var (_, client) = Seeded();

        var put = await client.PutAsJsonAsync("/api/settings/language", new { language = "zh-CN" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        var putBody = await put.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("zh-CN", putBody.GetProperty("language").GetString());

        var get = await client.GetFromJsonAsync<JsonElement>("/api/settings/language");
        Assert.Equal("zh-CN", get.GetProperty("language").GetString());

        // Case-insensitive input normalizes to the canonical form.
        var lower = await client.PutAsJsonAsync("/api/settings/language", new { language = "en-us" });
        lower.EnsureSuccessStatusCode();
        var lowerBody = await client.GetFromJsonAsync<JsonElement>("/api/settings/language");
        Assert.Equal("en-US", lowerBody.GetProperty("language").GetString());
    }

    [Fact]
    public async Task Put_invalid_locale_returns_400_with_coded_error()
    {
        var (_, client) = Seeded();

        var put = await client.PutAsJsonAsync("/api/settings/language", new { language = "fr-FR" });

        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
        var body = await put.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("settings.unsupportedLanguage", body.GetProperty("errorCode").GetString());
        Assert.Contains("fr-FR", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Backend_errors_localize_to_the_configured_language()
    {
        var (_, client) = Seeded();
        await client.PutAsJsonAsync("/api/settings/language", new { language = "zh-CN" });

        // Invalid LLM options → 400 with a Chinese message, stable errorCode.
        var put = await client.PutAsJsonAsync("/api/settings/llm-options", new
        {
            baseUrl = "not a uri",
            apiKey = "",
            model = "",
        });
        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
        var body = await put.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("llm.baseUrlInvalid", body.GetProperty("errorCode").GetString());
        Assert.Contains("BaseUrl", body.GetProperty("message").GetString());

        // The same call in English returns the English message.
        await client.PutAsJsonAsync("/api/settings/language", new { language = "en-US" });
        var en = await client.PutAsJsonAsync("/api/settings/llm-options", new { baseUrl = "not a uri" });
        var enBody = await en.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("llm.baseUrlInvalid", enBody.GetProperty("errorCode").GetString());
        Assert.Contains("http(s)", enBody.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Theme_get_falls_back_to_system_and_put_round_trips()
    {
        var (_, client) = Seeded();

        var initial = await client.GetFromJsonAsync<JsonElement>("/api/settings/theme");
        Assert.Equal("system", initial.GetProperty("theme").GetString());

        var put = await client.PutAsJsonAsync("/api/settings/theme", new { theme = "dark" });
        Assert.Equal(HttpStatusCode.OK, put.StatusCode);
        Assert.Equal("dark", (await put.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("theme").GetString());

        var after = await client.GetFromJsonAsync<JsonElement>("/api/settings/theme");
        Assert.Equal("dark", after.GetProperty("theme").GetString());
    }

    [Fact]
    public async Task Theme_put_invalid_value_returns_400_with_coded_error()
    {
        var (_, client) = Seeded();

        var put = await client.PutAsJsonAsync("/api/settings/theme", new { theme = "blue" });

        Assert.Equal(HttpStatusCode.BadRequest, put.StatusCode);
        var body = await put.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("settings.unsupportedTheme", body.GetProperty("errorCode").GetString());
        Assert.Contains("blue", body.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Setup_errors_localize_to_the_configured_language()
    {
        var (_, client) = Seeded();
        await client.PutAsJsonAsync("/api/settings/language", new { language = "zh-CN" });

        var post = await client.PostAsJsonAsync("/api/setup", new
        {
            displayName = "Danvic",
            email = "not-an-email",
            password = "correct-horse-battery",
        });

        Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
        var body = await post.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("setup.emailInvalid", body.GetProperty("errorCode").GetString());
        Assert.Contains("邮箱", body.GetProperty("message").GetString());
    }
}
