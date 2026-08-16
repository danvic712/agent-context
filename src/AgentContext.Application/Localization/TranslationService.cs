using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentContext.Application.Contracts;
using AgentContext.Application.Localization;

namespace AgentContext.Application.Localization;

/// <inheritdoc cref="ITranslationService"/>
/// <summary>
/// Loads every embedded <c>i18n.*.json</c> resource (the same physical files the
/// frontend bundles via Vite — ADR 0008) into an in-memory dictionary keyed by
/// locale, then resolves dotted keys with an en-US → raw-key fallback chain.
/// </summary>
public sealed class TranslationService : ITranslationService
{
    private readonly IReadOnlyDictionary<string, JsonObject> _resources;

    /// <summary>Loads all embedded localization resources from the Application assembly.</summary>
    public TranslationService() : this(typeof(TranslationService).Assembly)
    {
    }

    /// <summary>Loads all embedded localization resources from <paramref name="resourceAssembly"/>.</summary>
    public TranslationService(Assembly resourceAssembly)
    {
        var resources = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in resourceAssembly.GetManifestResourceNames()
                     .Where(n => n.StartsWith("i18n.", StringComparison.Ordinal))
                     .OrderBy(n => n, StringComparer.Ordinal))
        {
            // "i18n.en-US.json" → "en-US"
            var locale = name["i18n.".Length..].EndsWith(".json", StringComparison.Ordinal)
                ? name["i18n.".Length..^5]
                : name["i18n.".Length..];

            using var stream = resourceAssembly.GetManifestResourceStream(name);
            if (stream is null)
            {
                continue;
            }

            using var reader = new StreamReader(stream);
            if (JsonNode.Parse(reader.ReadToEnd()) is JsonObject document)
            {
                resources[locale] = document;
            }
        }

        _resources = resources;
    }

    public string Get(string key, string locale, params object?[] args)
    {
        var value = Lookup(key, LocalizationDefaults.Normalize(locale))
                    ?? Lookup(key, LocalizationDefaults.DefaultLocale)
                    ?? key;

        return args.Length > 0 ? string.Format(value, args) : value;
    }

    public string GetError(string errorCode, string locale, params object?[] args)
        => Get($"errors.{errorCode}", locale, args);

    public string ResolveLocale(string? locale) => LocalizationDefaults.Normalize(locale);

    private string? Lookup(string key, string locale)
    {
        if (!_resources.TryGetValue(locale, out var document))
        {
            return null;
        }

        JsonNode? current = document;
        foreach (var segment in key.Split('.'))
        {
            current = current is JsonObject obj ? obj[segment] : null;
            if (current is null)
            {
                return null;
            }
        }

        return current is JsonValue value ? value.GetValue<string>() : null;
    }
}
