using AndreGoepel.Marten.Identity.Roles;
using AndreGoepel.Marten.Identity.UserRoles;
using AndreGoepel.Marten.Identity.Users;
using Marten;
using RoleNames = AndreGoepel.Marten.Identity.Roles.Roles;

namespace AndreGoepel.Marten.Identity;

/// <summary>
/// Single source of truth for "is initial setup complete?". The Setup page,
/// the redirect middleware, and the nav menu must all agree, or users get
/// stuck in inconsistent states (e.g. middleware says "configured" but the
/// app has no Administrator role to authorise the admin pages).
/// </summary>
public static class SetupCompletion
{
    public static async Task<bool> IsCompleteAsync(
        IQuerySession session,
        CancellationToken cancellationToken = default
    )
    {
        var administratorRoleName = RoleNames.Administrator.ToUpperInvariant();
        var administratorRole = await session
            .Query<Role>()
            .Where(r => r.NormalizedName == administratorRoleName && !r.Deleted)
            .FirstOrDefaultAsync(cancellationToken);
        if (administratorRole is null)
            return false;

        var administratorRoleId = administratorRole.RoleId;

        // Setup is only complete once a non-deleted user actually *holds* the
        // Administrator role — not merely that the role exists and some user
        // exists. Checking existence alone (the previous behaviour) let a
        // mis-sequenced setup latch "configured" while leaving the admin pages
        // unreachable (#12), and meant the first-run window could close without a
        // usable administrator (#21).
        var holderIds = await session
            .Query<UserRoleAssignment>()
            .Where(a => a.RoleGuid == administratorRoleId)
            .Select(a => a.UserGuid)
            .ToListAsync(cancellationToken);
        if (holderIds.Count == 0)
            return false;

        return await session
            .Query<User>()
            .AnyAsync(u => holderIds.Contains(u.StreamId) && !u.Deleted, cancellationToken);
    }
}
