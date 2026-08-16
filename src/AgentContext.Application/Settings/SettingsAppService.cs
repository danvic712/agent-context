using AgentContext.Application.Contracts;
using AgentContext.Application.Learning;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AgentContext.Application.Settings;

/// <inheritdoc cref="ISettingsAppService"/>
/// <summary>
/// Reads and writes platform settings in the <c>settings</c> key/value table.
/// The LLM endpoint (ADR 0003) is stored as four keys; a missing/invalid set
/// reads back as <c>null</c> so the Learning Engine idles until configured.
/// </summary>
public sealed class SettingsAppService(AgentContextDbContext db) : ISettingsAppService
{
    public async Task<LlmOptions?> GetLlmOptionsAsync(CancellationToken cancellationToken = default)
    {
        var entries = await db.AppSettings.AsNoTracking().ToListAsync(cancellationToken);
        var get = (string key) => entries.FirstOrDefault(e => e.Key == key)?.Value;

        var baseUrl = get(SettingKeys.LlmBaseUrl);
        var apiKey = get(SettingKeys.LlmApiKey);
        var model = get(SettingKeys.LlmModel);

        if (string.IsNullOrWhiteSpace(baseUrl) || string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(model))
        {
            return null;
        }

        var options = new LlmOptions
        {
            BaseUrl = baseUrl,
            ApiKey = apiKey,
            Model = model,
            EmbeddingModel = get(SettingKeys.LlmEmbeddingModel),
        };

        return LlmOptions.Validate(options).Count == 0 ? options : null;
    }

    public async Task SaveLlmOptionsAsync(LlmOptions options, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);

        var entries = await db.AppSettings.ToListAsync(cancellationToken);

        // A blank ApiKey means "keep the existing key" — the UI never returns the
        // key (only a masked preview), so a partial update (e.g. model-only) must
        // not wipe it. Resolve before validating: when the store already has a key
        // the blank input inherits it; a genuinely first-time save still requires
        // one (validation below reports it).
        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            var existingKey = entries.FirstOrDefault(e => e.Key == SettingKeys.LlmApiKey)?.Value;
            if (!string.IsNullOrWhiteSpace(existingKey))
            {
                options.ApiKey = existingKey;
            }
        }

        var errors = LlmOptions.Validate(options);
        if (errors.Count > 0)
        {
            throw new ArgumentException(string.Join("; ", errors), nameof(options));
        }
        var upsert = (string key, string value) =>
        {
            var existing = entries.FirstOrDefault(e => e.Key == key);
            if (existing is null)
            {
                db.AppSettings.Add(new AppSetting { Key = key, Value = value });
            }
            else
            {
                existing.Value = value;
            }
        };

        upsert(SettingKeys.LlmBaseUrl, options.BaseUrl.Trim());
        upsert(SettingKeys.LlmApiKey, options.ApiKey.Trim());
        upsert(SettingKeys.LlmModel, options.Model.Trim());

        // Only persist a dedicated embedding model when it differs from the
        // extraction model — keeps the stored shape minimal (EffectiveEmbeddingModel
        // falls back to Model on read).
        var storedEmbeddingModel = entries.FirstOrDefault(e => e.Key == SettingKeys.LlmEmbeddingModel);
        if (!string.IsNullOrWhiteSpace(options.EmbeddingModel) && options.EmbeddingModel != options.Model)
        {
            if (storedEmbeddingModel is null)
            {
                db.AppSettings.Add(new AppSetting { Key = SettingKeys.LlmEmbeddingModel, Value = options.EmbeddingModel.Trim() });
            }
            else
            {
                storedEmbeddingModel.Value = options.EmbeddingModel.Trim();
            }
        }
        else if (storedEmbeddingModel is not null)
        {
            db.AppSettings.Remove(storedEmbeddingModel);
        }

        await db.SaveChangesAsync(cancellationToken);
    }
}
