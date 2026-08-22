using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Application.Localization;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace AgentContext.Application.Inference;

/// <inheritdoc cref="IInferenceConfigurationAppService" />
public sealed class InferenceConfigurationAppService(
    AgentContextDbContext db,
    IInferenceSecretProtector secrets,
    IHttpClientFactory httpClientFactory) : IInferenceConfigurationAppService
{
    private const string OpenAiCompatibleProviderType = "openai-compatible";
    private const int EmbeddingDimension = 1536;

    public async Task<InferenceConfigurationDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var configuration = await db.InferenceConfigurations
            .AsNoTracking()
            .Include(item => item.Routes)
            .OrderBy(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var providers = await db.InferenceProviders
            .AsNoTracking()
            .OrderBy(provider => provider.CreatedAtUtc)
            .ToListAsync(cancellationToken);

        if (configuration is null)
        {
            return new InferenceConfigurationDto(false, null, null, providers.Select(ToDto).ToArray(), [], null);
        }

        return new InferenceConfigurationDto(
            true,
            configuration.Id,
            configuration.Name,
            providers.Select(ToDto).ToArray(),
            configuration.Routes
                .OrderBy(route => route.Capability)
                .Select(route => new InferenceRouteDto(route.Id, route.Capability, route.ProviderId, route.Model))
                .ToArray(),
            configuration.UpdatedAtUtc);
    }

    public async Task<InferenceRuntimeOptions?> GetRuntimeOptionsAsync(
        CancellationToken cancellationToken = default)
    {
        var configuration = await db.InferenceConfigurations
            .AsNoTracking()
            .Include(item => item.Routes)
            .OrderBy(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);
        if (configuration is null || configuration.Routes.Count != 2)
        {
            return null;
        }

        var providerIds = configuration.Routes.Select(route => route.ProviderId).ToArray();
        var providers = await db.InferenceProviders
            .AsNoTracking()
            .Where(provider => providerIds.Contains(provider.Id))
            .ToDictionaryAsync(provider => provider.Id, cancellationToken);

        var resolved = new Dictionary<InferenceCapability, InferenceRuntimeRoute>();
        foreach (var route in configuration.Routes)
        {
            if (!providers.TryGetValue(route.ProviderId, out var provider))
            {
                return null;
            }

            var apiKey = secrets.Unprotect(provider.ApiKeySecretRef);
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return null;
            }

            resolved[route.Capability] = new InferenceRuntimeRoute(route.Id, provider.BaseUrl, apiKey, route.Model);
        }

        return resolved.TryGetValue(InferenceCapability.Chat, out var chat) &&
               resolved.TryGetValue(InferenceCapability.Embedding, out var embedding)
            ? new InferenceRuntimeOptions(chat, embedding)
            : null;
    }

    public async Task<InferenceConfigurationDto> SaveAsync(
        InferenceConfigurationInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);

        var validation = await VerifyAsync(input, cancellationToken);
        if (!validation.Valid)
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Inference.ValidationFailed);
        }

        var ownTransaction = db.Database.CurrentTransaction is null;
        await using var transaction = ownTransaction
            ? await db.Database.BeginTransactionAsync(cancellationToken)
            : null;

        var now = DateTimeOffset.UtcNow;
        var configuration = await db.InferenceConfigurations
            .Include(item => item.Routes)
            .OrderBy(item => item.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        if (configuration is null)
        {
            configuration = new InferenceConfiguration
            {
                Name = input.Name.Trim(),
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
            db.InferenceConfigurations.Add(configuration);
        }
        else
        {
            configuration.Name = input.Name.Trim();
            configuration.UpdatedAtUtc = now;
            db.InferenceRoutes.RemoveRange(configuration.Routes);
        }

        var existingProviders = await db.InferenceProviders.ToListAsync(cancellationToken);
        var inputProviderIds = input.Providers.Select(provider => provider.Id).ToHashSet();

        foreach (var provider in existingProviders.Where(provider => !inputProviderIds.Contains(provider.Id)))
        {
            db.InferenceProviders.Remove(provider);
        }

        foreach (var providerInput in input.Providers)
        {
            var provider = existingProviders.FirstOrDefault(item => item.Id == providerInput.Id);
            var apiKey = await ResolveApiKeyAsync(providerInput, provider);

            if (provider is null)
            {
                provider = new InferenceProvider
                {
                    Id = providerInput.Id,
                    CreatedAtUtc = now,
                };
                db.InferenceProviders.Add(provider);
            }

            provider.Name = providerInput.Name.Trim();
            provider.ProviderType = OpenAiCompatibleProviderType;
            provider.BaseUrl = NormalizeBaseUrl(providerInput.BaseUrl);
            provider.ApiKeySecretRef = secrets.Protect(apiKey!);
            provider.UpdatedAtUtc = now;
        }

        db.InferenceRoutes.AddRange(input.Routes.Select(route => new InferenceRoute
        {
            Id = route.Id,
            InferenceConfiguration = configuration,
            Capability = route.Capability,
            ProviderId = route.ProviderId,
            Model = route.Model.Trim(),
        }));

        await db.SaveChangesAsync(cancellationToken);
        if (ownTransaction)
        {
            await transaction!.CommitAsync(cancellationToken);
        }

        return await GetAsync(cancellationToken);
    }

    public async Task<InferenceValidationResult> VerifyAsync(
        InferenceConfigurationInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        ValidateShape(input);

        var existingProviders = await db.InferenceProviders
            .Where(provider => input.Providers.Select(item => item.Id).Contains(provider.Id))
            .ToDictionaryAsync(provider => provider.Id, cancellationToken);

        var credentials = new Dictionary<Guid, string>();
        foreach (var provider in input.Providers)
        {
            var existing = existingProviders.GetValueOrDefault(provider.Id);
            var key = await ResolveApiKeyAsync(provider, existing);
            if (string.IsNullOrWhiteSpace(key))
            {
                throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Inference.ApiKeyRequired);
            }

            credentials[provider.Id] = key;
        }

        var checks = new List<InferenceValidationCheck>(2);
        foreach (var capability in new[] { InferenceCapability.Chat, InferenceCapability.Embedding })
        {
            var route = input.Routes.Single(item => item.Capability == capability);
            var provider = input.Providers.Single(item => item.Id == route.ProviderId);
            checks.Add(await ProbeAsync(capability, route, provider, credentials[provider.Id], cancellationToken));
        }

        return new InferenceValidationResult(checks.All(check => check.Valid), checks);
    }

    private async Task<InferenceValidationCheck> ProbeAsync(
        InferenceCapability capability,
        InferenceRouteInput route,
        InferenceProviderInput provider,
        string apiKey,
        CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                $"{NormalizeBaseUrl(provider.BaseUrl)}/{EndpointFor(capability)}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            request.Content = capability == InferenceCapability.Chat
                ? JsonContent.Create(new
                {
                    model = route.Model.Trim(),
                    messages = new[] { new { role = "user", content = "Reply with OK." } },
                    max_tokens = 4,
                    temperature = 0,
                })
                : JsonContent.Create(new
                {
                    model = route.Model.Trim(),
                    input = "inference-configuration-validation",
                    dimensions = EmbeddingDimension,
                });

            using var response = await httpClientFactory.CreateClient("inference-validation")
                .SendAsync(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return new InferenceValidationCheck(
                    capability,
                    false,
                    $"The provider returned {(int)response.StatusCode} {response.ReasonPhrase}.");
            }

            await using var content = await response.Content.ReadAsStreamAsync(cancellationToken);
            using var document = await JsonDocument.ParseAsync(content, cancellationToken: cancellationToken);
            if (capability == InferenceCapability.Chat && !HasChatCompletion(document))
            {
                return new InferenceValidationCheck(capability, false, "The chat response did not contain a completion.");
            }

            if (capability == InferenceCapability.Embedding && !HasEmbeddingDimension(document, EmbeddingDimension))
            {
                return new InferenceValidationCheck(
                    capability,
                    false,
                    $"The embedding response must contain {EmbeddingDimension} dimensions.");
            }

            return new InferenceValidationCheck(capability, true, "Connection verified.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new InferenceValidationCheck(capability, false, "The provider request timed out.");
        }
        catch (HttpRequestException exception)
        {
            return new InferenceValidationCheck(capability, false, exception.Message);
        }
        catch (JsonException)
        {
            return new InferenceValidationCheck(capability, false, "The provider returned invalid JSON.");
        }
    }

    private Task<string?> ResolveApiKeyAsync(
        InferenceProviderInput input,
        InferenceProvider? existing)
    {
        if (!string.IsNullOrWhiteSpace(input.ApiKey))
        {
            return Task.FromResult<string?>(input.ApiKey.Trim());
        }

        return Task.FromResult(existing is null ? null : secrets.Unprotect(existing.ApiKeySecretRef));
    }

    private static void ValidateShape(InferenceConfigurationInput input)
    {
        if (string.IsNullOrWhiteSpace(input.Name))
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Inference.NameRequired);
        }

        if (input.Providers is null || input.Providers.Count == 0)
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Inference.ProviderRequired);
        }

        if (input.Providers.Any(provider => provider.Id == Guid.Empty))
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Inference.ProviderIdRequired);
        }

        if (input.Providers.Select(provider => provider.Id).Distinct().Count() != input.Providers.Count)
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Inference.ProviderIdDuplicate);
        }

        foreach (var provider in input.Providers)
        {
            if (string.IsNullOrWhiteSpace(provider.Name))
            {
                throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Inference.ProviderNameRequired);
            }

            if (!string.Equals(provider.ProviderType, OpenAiCompatibleProviderType, StringComparison.OrdinalIgnoreCase))
            {
                throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Inference.ProviderTypeUnsupported);
            }

            if (!Uri.TryCreate(provider.BaseUrl?.Trim().TrimEnd('/'), UriKind.Absolute, out var uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Inference.BaseUrlInvalid);
            }
        }

        if (input.Routes is null || input.Routes.Count != 2)
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Inference.RoutesRequired);
        }

        if (input.Routes.GroupBy(route => route.Capability).Any(group => group.Count() > 1) ||
            input.Routes.Select(route => route.Capability).Distinct().Count() != 2 ||
            !input.Routes.Any(route => route.Capability == InferenceCapability.Chat) ||
            !input.Routes.Any(route => route.Capability == InferenceCapability.Embedding))
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Inference.CapabilitiesRequired);
        }

        if (input.Routes.Select(route => route.Id).Distinct().Count() != input.Routes.Count)
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Inference.RouteIdDuplicate);
        }

        foreach (var route in input.Routes)
        {
            if (route.Id == Guid.Empty)
            {
                throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Inference.RouteIdRequired);
            }

            if (!input.Providers.Any(provider => provider.Id == route.ProviderId))
            {
                throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Inference.ProviderNotFound);
            }

            if (string.IsNullOrWhiteSpace(route.Model))
            {
                throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Inference.ModelRequired);
            }
        }
    }

    private static bool HasEmbeddingDimension(JsonDocument document, int expectedDimension)
        => document.RootElement.TryGetProperty("data", out var data) &&
           data.ValueKind == JsonValueKind.Array &&
           data.GetArrayLength() > 0 &&
           data[0].TryGetProperty("embedding", out var embedding) &&
           embedding.ValueKind == JsonValueKind.Array &&
           embedding.GetArrayLength() == expectedDimension;

    private static bool HasChatCompletion(JsonDocument document)
        => document.RootElement.TryGetProperty("choices", out var choices) &&
           choices.ValueKind == JsonValueKind.Array &&
           choices.GetArrayLength() > 0;

    private static string EndpointFor(InferenceCapability capability)
        => capability == InferenceCapability.Chat ? "chat/completions" : "embeddings";

    private static string NormalizeBaseUrl(string baseUrl) => baseUrl.Trim().TrimEnd('/');

    private static InferenceProviderDto ToDto(InferenceProvider provider)
        => new(
            provider.Id,
            provider.Name,
            provider.ProviderType,
            provider.BaseUrl,
            !string.IsNullOrWhiteSpace(provider.ApiKeySecretRef),
            string.IsNullOrWhiteSpace(provider.ApiKeySecretRef) ? null : "••••••",
            provider.CreatedAtUtc,
            provider.UpdatedAtUtc);
}
