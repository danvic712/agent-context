using System.Net;
using AgentContext.Domain;
using AgentContext.Domain.Entities;
using AgentContext.Infrastructure;
using Microsoft.EntityFrameworkCore;
using AgentContext.Application.Contracts;
using AgentContext.Application.Dtos;
using AgentContext.Application.Localization;

namespace AgentContext.Application.Setup;

/// <inheritdoc cref="ISetupAppService"/>
public sealed class SetupAppService(AgentContextDbContext db) : ISetupAppService
{
    public async Task<SetupStatus> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        var configured = await db.Users.AnyAsync(cancellationToken);
        return new SetupStatus(configured);
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

        await db.SaveChangesAsync(cancellationToken);
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
    }
}
