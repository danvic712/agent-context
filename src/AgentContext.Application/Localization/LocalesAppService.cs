using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using AgentContext.Application.Contracts;
using AgentContext.Application.Localization;

namespace AgentContext.Application.Localization;

/// <inheritdoc cref="ILocalesAppService"/>
/// <summary>
/// Loads every embedded <c>locales.*.json</c> resource (the same physical files
/// the frontend bundles via Vite — ADR 0008) into separate per-locale resources,
/// then resolves dotted keys with an en-US → raw-key fallback chain.
/// </summary>
public sealed class LocalesAppService : ILocalesAppService
{
    private const string ResourcePrefix = "locales.";
    private readonly IReadOnlyDictionary<string, IReadOnlyList<JsonObject>> _resources;

    /// <summary>Loads all embedded localization resources from the Application assembly.</summary>
    public LocalesAppService() : this(typeof(LocalesAppService).Assembly)
    {
    }

    /// <summary>Loads all embedded localization resources from <paramref name="resourceAssembly"/>.</summary>
    public LocalesAppService(Assembly resourceAssembly)
    {
        var resources = new Dictionary<string, List<JsonObject>>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in resourceAssembly.GetManifestResourceNames()
                     .Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal))
                     .OrderBy(n => n, StringComparer.Ordinal))
        {
            // "locales.en-US.common.json" → "en-US"
            var resourcePath = name[ResourcePrefix.Length..];
            var separator = resourcePath.IndexOf('.', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var locale = resourcePath[..separator];

            using var stream = resourceAssembly.GetManifestResourceStream(name);
            if (stream is null)
            {
                continue;
            }

            using var reader = new StreamReader(stream);
            if (JsonNode.Parse(reader.ReadToEnd()) is JsonObject document)
            {
                if (!resources.TryGetValue(locale, out var localeResources))
                {
                    localeResources = [];
                    resources[locale] = localeResources;
                }

                localeResources.Add(document);
            }
        }

        _resources = resources.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlyList<JsonObject>)pair.Value.ToArray(),
            StringComparer.OrdinalIgnoreCase);
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
        if (!_resources.TryGetValue(locale, out var documents))
        {
            return null;
        }

        foreach (var document in documents)
        {
            JsonNode? current = document;
            foreach (var segment in key.Split('.'))
            {
                current = current is JsonObject obj ? obj[segment] : null;
                if (current is null)
                {
                    break;
                }
            }

            if (current is JsonValue value)
            {
                return value.GetValue<string>();
            }
        }

        return null;
    }
}
