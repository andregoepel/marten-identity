using AndreGoepel.Marten.Identity.Roles;
using AndreGoepel.Marten.Identity.UserRoles;
using Marten;
using RoleNames = AndreGoepel.Marten.Identity.Roles.Roles;

namespace AndreGoepel.Marten.Identity.Services;

/// <inheritdoc />
public sealed class IdentityAuthorizer(
    ICurrentUserService currentUserService,
    IQuerySession querySession
) : IIdentityAuthorizer
{
    private static readonly string NormalizedAdministrator =
        RoleNames.Administrator.ToUpperInvariant();

    // Ambient across the async flow; each execution context carries its own value, so a
    // system scope on one request never bleeds into another.
    private static readonly AsyncLocal<bool> SystemScopeFlag = new();

    public bool IsSystemScope => SystemScopeFlag.Value;

    public IDisposable BeginSystemScope()
    {
        var previous = SystemScopeFlag.Value;
        SystemScopeFlag.Value = true;
        return new Scope(() => SystemScopeFlag.Value = previous);
    }

    public async Task<bool> IsCurrentUserAdministratorAsync(
        CancellationToken cancellationToken = default
    )
    {
        if (IsSystemScope)
            return true;

        var actor = await currentUserService.GetCurrentUserIdAsync(cancellationToken);

        // Fail closed: an unidentified caller is never treated as an administrator.
        if (actor.Value == Guid.Empty)
            return false;

        // DB-authoritative: check the live projection, not claims (#41 — the acting
        // identity from claims proves identity, never authority).
        var adminRole = await querySession
            .Query<Role>()
            .FirstOrDefaultAsync(
                r => r.NormalizedName == NormalizedAdministrator && !r.Deleted,
                cancellationToken
            );
        if (adminRole is null)
            return false;

        return await querySession
            .Query<UserRoleAssignment>()
            .AnyAsync(
                a => a.UserGuid == actor.Value && a.RoleGuid == adminRole.RoleId,
                cancellationToken
            );
    }

    private sealed class Scope(Action onDispose) : IDisposable
    {
        private Action? _onDispose = onDispose;

        public void Dispose()
        {
            _onDispose?.Invoke();
            _onDispose = null;
        }
    }
}
