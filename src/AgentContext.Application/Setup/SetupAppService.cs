using System.Net;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;
using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Application.Localization;
using AgentContext.Application.Settings;

namespace AgentContext.Application.Setup;

/// <inheritdoc cref="ISetupAppService"/>
public sealed class SetupAppService(
    AgentContextDbContext db,
    IInferenceConfigurationAppService? inference = null) : ISetupAppService
{
    public async Task<SetupStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var configured = await db.Users.AnyAsync(cancellationToken);
        var workspaceName = configured
            ? await db.Workspaces
                .AsNoTracking()
                .OrderBy(workspace => workspace.CreatedAtUtc)
                .Select(workspace => workspace.Name)
                .FirstOrDefaultAsync(cancellationToken)
            : null;
        return new SetupStatus(configured, workspaceName);
    }

    public async Task<SetupResult> ConfigureAsync(SetupRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        Validate(request);

        if (await db.Users.AnyAsync(cancellationToken))
        {
            throw new LocalizedException(HttpStatusCode.Conflict, ErrorCodes.Setup.AlreadyConfigured);
        }

        var now = DateTimeOffset.UtcNow;

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var user = new User
        {
            DisplayName = request.DisplayName.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = PasswordHasher.Hash(request.Password),
            CreatedAtUtc = now,
        };
        db.Users.Add(user);

        var workspace = new Workspace
        {
            Name = $"{user.DisplayName}'s Workspace",
            Type = WorkspaceType.Personal,
            CreatedAtUtc = now,
        };
        db.Workspaces.Add(workspace);

        db.Memberships.Add(new Membership
        {
            Workspace = workspace,
            User = user,
            Role = MembershipRole.Admin,
            CreatedAtUtc = now,
        });

        LocalizationDefaults.TryNormalize(request.Language, out var language);
        db.AppSettings.Add(new AppSetting { Key = SettingKeys.Language, Value = language });

        await db.SaveChangesAsync(cancellationToken);
        if (HasInferenceInput(request.InferenceConfiguration))
        {
            if (inference is null)
            {
                throw new InvalidOperationException("Inference configuration service is not available.");
            }

            // The inference service joins the current transaction, so account,
            // workspace, membership, and model configuration are all-or-nothing.
            await inference.SaveAsync(request.InferenceConfiguration!, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);

        return new SetupResult(user.Id, workspace.Id, workspace.Name);
    }

    private static void Validate(SetupRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.DisplayName))
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Setup.DisplayNameRequired);
        }

        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@'))
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Setup.EmailInvalid);
        }

        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Setup.PasswordTooShort);
        }

        if (!LocalizationDefaults.TryNormalize(request.Language, out _))
        {
            throw new LocalizedException(HttpStatusCode.BadRequest, ErrorCodes.Settings.UnsupportedLanguage, request.Language);
        }
    }

    private static bool HasInferenceInput(InferenceConfigurationInput? configuration)
        => configuration is not null &&
           (configuration.Routes?.Any(route => !string.IsNullOrWhiteSpace(route.Model)) == true ||
            configuration.Providers?.Any(provider => !string.IsNullOrWhiteSpace(provider.ApiKey)) == true);
}
