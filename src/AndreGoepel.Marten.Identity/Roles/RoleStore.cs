using AndreGoepel.Marten.Identity.Roles.Events;
using AndreGoepel.Marten.Identity.Services;
using Marten;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;

namespace AndreGoepel.Marten.Identity.Roles;

public class RoleStore<TRole>(
    IDocumentSession session,
    ICurrentUserService currentUserService,
    IIdentityAuthorizer authorizer,
    ILogger<RoleStore<TRole>> logger
) : IQueryableRoleStore<TRole>
    where TRole : Role
{
    public IQueryable<TRole> Roles => session.Query<TRole>();

    // Appended when the caller is unidentified rather than merely non-admin, because that
    // almost always means an unscoped first-run bootstrap. Points at the escape hatch so
    // the failure is self-diagnosing instead of a silent fail-closed (#101).
    private const string BootstrapHint =
        " No user is authenticated; if this is a system or first-run operation, "
        + "wrap it in IIdentityAuthorizer.BeginSystemScope().";

    // Defence in depth (#69/#41): role management is an administrator-only operation,
    // enforced here independently of any UI [Authorize] guard. The rejection distinguishes
    // "not an administrator" from "nobody is authenticated" so a bootstrapping consumer is
    // steered to BeginSystemScope rather than hunting for a missing role assignment (#101).
    private async Task<IdentityResult> NotAuthorizedAsync(
        string operation,
        CancellationToken cancellationToken
    )
    {
        var actor = await currentUserService.GetCurrentUserIdAsync(cancellationToken);
        var unidentified = actor.Value == Guid.Empty;
        var description =
            $"{operation} a role requires administrator authority."
            + (unidentified ? BootstrapHint : string.Empty);

        // Surface the reason in the host's logs even when the consumer swallows the
        // IdentityResult — the original trap (app-foundation#89) took a SQL log to diagnose.
        logger.LogWarning(
            "Role management rejected: {Operation} requires administrator authority "
                + "(caller {CallerState}).",
            operation,
            unidentified ? "unidentified" : "non-administrator"
        );

        return IdentityResult.Failed(
            new IdentityError { Code = IdentityErrorCodes.NotAuthorized, Description = description }
        );
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public async Task<IdentityResult> CreateAsync(TRole role, CancellationToken cancellationToken)
    {
        try
        {
            if (!await authorizer.IsCurrentUserAdministratorAsync(cancellationToken))
                return await NotAuthorizedAsync("Creating", cancellationToken);

            if (role.Name == null)
                return IdentityResult.Failed(
                    new IdentityError
                    {
                        Code = IdentityErrorCodes.RoleNameRequired,
                        Description = "Role name cannot be null.",
                    }
                );

            session.Events.Append(
                role.StreamId,
                new RoleCreated(
                    role.RoleId,
                    role.Name,
                    await currentUserService.GetCurrentUserIdAsync(cancellationToken)
                )
                {
                    Deletable = role.Deletable,
                }
            );
            await session.SaveChangesAsync(cancellationToken);

            return IdentityResult.Success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create the role in Marten.");
            return IdentityResult.Failed(
                new IdentityError
                {
                    Code = IdentityErrorCodes.RoleSaveFailed,
                    Description = "Something went wrong saving the role.",
                }
            );
        }
    }

    public async Task<IdentityResult> UpdateAsync(TRole role, CancellationToken cancellationToken)
    {
        try
        {
            if (!await authorizer.IsCurrentUserAdministratorAsync(cancellationToken))
                return await NotAuthorizedAsync("Updating", cancellationToken);

            if (role.Name == null)
            {
                return IdentityResult.Failed(
                    new IdentityError
                    {
                        Code = IdentityErrorCodes.RoleNameRequired,
                        Description = "Role name cannot be null.",
                    }
                );
            }

            session.Events.Append(
                role.RoleId,
                new RoleChanged(
                    role.RoleId,
                    role.Name,
                    await currentUserService.GetCurrentUserIdAsync(cancellationToken)
                )
                {
                    Deletable = role.Deletable,
                }
            );

            await session.SaveChangesAsync(cancellationToken);

            return IdentityResult.Success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update the role in Marten.");
            return IdentityResult.Failed(
                new IdentityError
                {
                    Code = IdentityErrorCodes.RoleSaveFailed,
                    Description = "Something went wrong saving the role.",
                }
            );
        }
    }

    public async Task<IdentityResult> DeleteAsync(TRole role, CancellationToken cancellationToken)
    {
        try
        {
            if (!await authorizer.IsCurrentUserAdministratorAsync(cancellationToken))
                return await NotAuthorizedAsync("Deleting", cancellationToken);

            if (!role.Deletable)
            {
                return IdentityResult.Failed(
                    new IdentityError
                    {
                        Code = IdentityErrorCodes.RoleNotDeletable,
                        Description = "This role cannot be deleted.",
                    }
                );
            }

            session.Events.Append(
                role.StreamId,
                new RoleDeleted(
                    role.RoleId,
                    await currentUserService.GetCurrentUserIdAsync(cancellationToken)
                )
            );
            await session.SaveChangesAsync(cancellationToken);

            return IdentityResult.Success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to delete the role in Marten.");
            return IdentityResult.Failed(
                new IdentityError
                {
                    Code = IdentityErrorCodes.RoleDeleteFailed,
                    Description = "Something went wrong deleting the role.",
                }
            );
        }
    }

    public async Task<IdentityResult> RestoreAsync(
        TRole role,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            if (!await authorizer.IsCurrentUserAdministratorAsync(cancellationToken))
                return await NotAuthorizedAsync("Restoring", cancellationToken);

            session.Events.Append(
                role.StreamId,
                new RoleRestored(
                    role.RoleId,
                    await currentUserService.GetCurrentUserIdAsync(cancellationToken)
                )
            );
            await session.SaveChangesAsync(cancellationToken);

            return IdentityResult.Success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to restore the role in Marten.");
            return IdentityResult.Failed(
                new IdentityError
                {
                    Code = IdentityErrorCodes.RoleRestoreFailed,
                    Description = "Something went wrong restoring the role.",
                }
            );
        }
    }

    public Task<string> GetRoleIdAsync(TRole role, CancellationToken cancellationToken) =>
        Task.FromResult(role.Id);

    public Task<string?> GetRoleNameAsync(TRole role, CancellationToken cancellationToken) =>
        Task.FromResult(role.Name);

    public Task SetRoleNameAsync(TRole role, string? roleName, CancellationToken cancellationToken)
    {
        role.Name = roleName;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedRoleNameAsync(
        TRole role,
        CancellationToken cancellationToken
    ) => Task.FromResult(role.NormalizedName);

    public Task SetNormalizedRoleNameAsync(
        TRole role,
        string? normalizedName,
        CancellationToken cancellationToken
    )
    {
        role.NormalizedName = normalizedName;
        return Task.CompletedTask;
    }

    public async Task<TRole?> FindByIdAsync(string roleId, CancellationToken cancellationToken) =>
        await session.Query<TRole>().FirstOrDefaultAsync(x => x.Id == roleId, cancellationToken);

    public async Task<TRole?> FindByNameAsync(
        string? normalizedRoleName,
        CancellationToken cancellationToken
    ) =>
        await session
            .Query<TRole>()
            .FirstOrDefaultAsync(x => x.NormalizedName == normalizedRoleName, cancellationToken);
}
