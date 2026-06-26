using System.Security.Cryptography;
using System.Text;
using AndreGoepel.Marten.Identity.Roles;
using AndreGoepel.Marten.Identity.Services;
using AndreGoepel.Marten.Identity.UserRoles;
using AndreGoepel.Marten.Identity.Users.Events;
using Marten;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RoleNames = AndreGoepel.Marten.Identity.Roles.Roles;

namespace AndreGoepel.Marten.Identity.Users;

public class UserStore<TUser>(
    IDocumentStore documentStore,
    IQuerySession querySession,
    IDataProtectionProvider dataProtectionProvider,
    ICurrentUserService currentUserService,
    IOptions<IdentityOptions> identityOptions,
    ILogger<UserStore<TUser>> logger
)
    : IUserStore<TUser>,
        IUserPasswordStore<TUser>,
        IUserEmailStore<TUser>,
        IUserPhoneNumberStore<TUser>,
        IUserTwoFactorStore<TUser>,
        IUserAuthenticatorKeyStore<TUser>,
        IUserTwoFactorRecoveryCodeStore<TUser>,
        IUserSecurityStampStore<TUser>,
        IQueryableUserStore<TUser>,
        IUserPasskeyStore<TUser>,
        IUserRoleStore<TUser>,
        IUserLockoutStore<TUser>
    where TUser : User
{
    private const string _userDataProtectionPurpose = "UserDataProtection";

    public IQueryable<TUser> Users => querySession.Query<TUser>();

    public Task<string> GetUserIdAsync(TUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.Id);

    public Task<string?> GetUserNameAsync(TUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.UserName);

    public Task SetUserNameAsync(TUser user, string? userName, CancellationToken cancellationToken)
    {
        user.UserName = userName;
        return Task.CompletedTask;
    }

    public Task<string?> GetNormalizedUserNameAsync(
        TUser user,
        CancellationToken cancellationToken
    ) => Task.FromResult(user.NormalizedUserName);

    public Task SetNormalizedUserNameAsync(
        TUser user,
        string? normalizedName,
        CancellationToken cancellationToken
    )
    {
        user.NormalizedUserName = normalizedName;
        return Task.CompletedTask;
    }

    public async Task<IdentityResult> CreateAsync(TUser user, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var userId = UserId.Parse(user.Id);

            // Enable lockout for newly created accounts so that brute-force
            // protection (SignInManager's lockoutOnFailure) actually engages.
            // Honour the host's policy via Lockout.AllowedForNewUsers.
            // The root user (created during first-run setup) is exempt: locking
            // it out would lock the only administrator out of the application.
            user.LockoutEnabled =
                !user.RootUser && identityOptions.Value.Lockout.AllowedForNewUsers;

            using var session = documentStore.LightweightSession();

            session.Events.Append(
                userId.Value,
                new UserCreated(userId, user.UserName, user.Email, user.PasswordHash)
                {
                    RootUser = user.RootUser,
                    Deletable = user.Deletable,
                    EmailConfirmed = user.EmailConfirmed,
                    LockoutEnabled = user.LockoutEnabled,
                    SecurityStamp = user.SecurityStamp,
                }
            );
            await session.SaveChangesAsync(cancellationToken);
            return IdentityResult.Success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create the user in Marten.");
            return IdentityResult.Failed(
                new IdentityError() { Description = "Something went wrong saving the user." }
            );
        }
    }

    public async Task<IdentityResult> UpdateAsync(TUser user, CancellationToken cancellationToken)
    {
        try
        {
            var userId = UserId.Parse(user.Id);

            var existingUser = await querySession
                .Query<TUser>()
                .FirstOrDefaultAsync(x => x.Id == user.Id, token: cancellationToken);

            if (existingUser != null)
            {
                // Lockout state (AccessFailedCount / LockoutEnd) is owned exclusively
                // by the atomic IUserLockoutStore methods, which serialize writes on
                // the stream. The generic update path runs from a request-start
                // snapshot with no concurrency control, so it must never carry a stale
                // lockout value back into the stream — doing so would resurrect the
                // failed-login accumulation race (#22). Treat the persisted values as
                // authoritative here.
                user.AccessFailedCount = existingUser.AccessFailedCount;
                user.LockoutEnd = existingUser.LockoutEnd;

                // Domain-layer invariant (#41): the root user must stay non-deletable so
                // it can never be removed (which would orphan administration). Don't let
                // a generic update flip Deletable, regardless of who initiates it.
                if (existingUser.RootUser)
                    user.Deletable = false;
            }

            if (existingUser != null && existingUser.AreEqual(user))
                return IdentityResult.Success;

            using var session = documentStore.LightweightSession();

            session.Events.Append(
                userId.Value,
                new UserUpdated(userId)
                {
                    UserName = user.UserName,
                    Email = user.Email,
                    PasswordHash = user.PasswordHash,
                    EmailConfirmed = user.EmailConfirmed,
                    PhoneNumber = user.PhoneNumber,
                    AuthenticatorKey = user.AuthenticatorKey,
                    RecoveryCodes = user.RecoveryCodes,
                    TwoFactorEnabled = user.TwoFactorEnabled,
                    Deletable = user.Deletable,
                    LockoutEnabled = user.LockoutEnabled,
                    LockoutEnd = user.LockoutEnd,
                    AccessFailedCount = user.AccessFailedCount,
                    SecurityStamp = user.SecurityStamp,
                }
            );
            await session.SaveChangesAsync(cancellationToken);
            return IdentityResult.Success;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to update the user in Marten.");
            return IdentityResult.Failed(
                new IdentityError() { Description = "Something went wrong saving the user." }
            );
        }
    }

    public async Task<IdentityResult> DeleteAsync(TUser user, CancellationToken cancellationToken)
    {
        try
        {
            if (!user.Deletable)
            {
                return IdentityResult.Failed(
                    new IdentityError() { Description = "This user cannot be deleted." }
                );
            }

            var userId = UserId.Parse(user.Id);

            using var session = documentStore.LightweightSession();

            session.Events.Append(userId.Value, new UserDeleted(userId));
            await session.SaveChangesAsync(cancellationToken);
            return IdentityResult.Success;
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "Failed to delete the user in Marten.");
            }
            return IdentityResult.Failed(
                new IdentityError() { Description = "Something went wrong deleting the user." }
            );
        }
    }

    public async Task<IdentityResult> RestoreAsync(
        TUser user,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var userId = UserId.Parse(user.Id);

            var stream = await querySession.Events.FetchStreamAsync(
                userId.Value,
                token: cancellationToken
            );

            var deletionVersion =
                stream.LastOrDefault(e => e.Data is UserDeleted)?.Version ?? stream.Count;
            var snapshot = new User();
            var projection = new UserProjection();

            foreach (var e in stream.Where(e => e.Version < deletionVersion))
            {
                switch (e.Data)
                {
                    case UserCreated created:
                        projection.Apply(created, snapshot);
                        break;
                    case UserUpdated updated:
                        projection.Apply(updated, snapshot);
                        break;
                    case UserRestored restored:
                        projection.Apply(restored, snapshot);
                        break;
                }
            }

            using var session = documentStore.LightweightSession();

            session.Events.Append(
                userId.Value,
                new UserRestored(userId, await currentUserService.GetCurrentUserIdAsync())
                {
                    UserName = snapshot.UserName,
                    Email = snapshot.Email,
                    PasswordHash = snapshot.PasswordHash,
                    SecurityStamp = snapshot.SecurityStamp,
                }
            );
            await session.SaveChangesAsync(cancellationToken);
            return IdentityResult.Success;
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Error))
            {
                logger.LogError(ex, "Failed to restore the user in Marten.");
            }
            return IdentityResult.Failed(
                new IdentityError() { Description = "Something went wrong restoring the user." }
            );
        }
    }

    public async Task<TUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
        await querySession
            .Query<TUser>()
            .FirstOrDefaultAsync(x => x.Id == userId, cancellationToken);

    public async Task<TUser?> FindByNameAsync(
        string normalizedUserName,
        CancellationToken cancellationToken
    ) =>
        await querySession
            .Query<TUser>()
            .FirstOrDefaultAsync(
                x => x.NormalizedUserName == normalizedUserName,
                cancellationToken
            );

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    // IUserPasswordStore

    public Task SetPasswordHashAsync(
        TUser user,
        string? passwordHash,
        CancellationToken cancellationToken
    )
    {
        user.PasswordHash = passwordHash;
        return Task.CompletedTask;
    }

    public Task<string?> GetPasswordHashAsync(TUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.PasswordHash);

    public Task<bool> HasPasswordAsync(TUser user, CancellationToken cancellationToken)
    {
        bool hasPassword = !string.IsNullOrEmpty(user.PasswordHash);
        return Task.FromResult(hasPassword);
    }

    // IUserSecurityStampStore

    public Task SetSecurityStampAsync(TUser user, string stamp, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        user.SecurityStamp = stamp;
        return Task.CompletedTask;
    }

    public Task<string?> GetSecurityStampAsync(TUser user, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(user);
        return Task.FromResult(user.SecurityStamp);
    }

    // IUserEmailStore

    public Task SetEmailAsync(TUser user, string? email, CancellationToken cancellationToken)
    {
        user.Email = email;
        return Task.CompletedTask;
    }

    public Task<string?> GetEmailAsync(TUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.Email);

    public Task<bool> GetEmailConfirmedAsync(TUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.EmailConfirmed);

    public Task SetEmailConfirmedAsync(
        TUser user,
        bool confirmed,
        CancellationToken cancellationToken
    )
    {
        user.EmailConfirmed = confirmed;
        return Task.CompletedTask;
    }

    public async Task<TUser?> FindByEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken
    ) =>
        await querySession
            .Query<TUser>()
            .FirstOrDefaultAsync(x => x.NormalizedEmail == normalizedEmail, cancellationToken);

    public Task<string?> GetNormalizedEmailAsync(TUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.NormalizedEmail);

    public Task SetNormalizedEmailAsync(
        TUser user,
        string? normalizedEmail,
        CancellationToken cancellationToken
    )
    {
        user.NormalizedEmail = normalizedEmail;
        return Task.CompletedTask;
    }

    // IUserPhoneNumberStore

    public Task SetPhoneNumberAsync(
        TUser user,
        string? phoneNumber,
        CancellationToken cancellationToken
    )
    {
        user.PhoneNumber = phoneNumber;
        return Task.CompletedTask;
    }

    public Task<string?> GetPhoneNumberAsync(TUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.PhoneNumber);

    public Task<bool> GetPhoneNumberConfirmedAsync(
        TUser user,
        CancellationToken cancellationToken
    ) => Task.FromResult(user.PhoneNumberConfirmed);

    public Task SetPhoneNumberConfirmedAsync(
        TUser user,
        bool confirmed,
        CancellationToken cancellationToken
    )
    {
        user.PhoneNumberConfirmed = confirmed;
        return Task.CompletedTask;
    }

    // IUserTwoFactorStore

    public Task SetTwoFactorEnabledAsync(
        TUser user,
        bool enabled,
        CancellationToken cancellationToken
    )
    {
        user.TwoFactorEnabled = enabled;
        return Task.CompletedTask;
    }

    public Task<bool> GetTwoFactorEnabledAsync(TUser user, CancellationToken cancellationToken)
    {
        return Task.FromResult(user.TwoFactorEnabled);
    }

    // IUserAuthenticatorKeyStore

    public Task SetAuthenticatorKeyAsync(
        TUser user,
        string key,
        CancellationToken cancellationToken
    )
    {
        var protector = dataProtectionProvider.CreateProtector(_userDataProtectionPurpose);

        user.AuthenticatorKey = protector.Protect(key);
        return Task.CompletedTask;
    }

    public Task<string?> GetAuthenticatorKeyAsync(TUser user, CancellationToken cancellationToken)
    {
        var protector = dataProtectionProvider.CreateProtector(_userDataProtectionPurpose);
        return Task.FromResult(
            user.AuthenticatorKey == null ? null : protector.Unprotect(user.AuthenticatorKey)
        );
    }

    // IUserTwoFactorRecoveryCodeStore

    public Task ReplaceCodesAsync(
        TUser user,
        IEnumerable<string> recoveryCodes,
        CancellationToken cancellationToken
    )
    {
        var protector = dataProtectionProvider.CreateProtector(_userDataProtectionPurpose);

        user.RecoveryCodes = protector.Protect(string.Join(';', recoveryCodes));
        return Task.CompletedTask;
    }

    public Task<bool> RedeemCodeAsync(TUser user, string code, CancellationToken cancellationToken)
    {
        var protector = dataProtectionProvider.CreateProtector(_userDataProtectionPurpose);

        var codes = (user.RecoveryCodes == null ? "" : protector.Unprotect(user.RecoveryCodes))
            .Split(';', StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        var idx = codes.FindIndex(c => FixedTimeEquals(c, code));
        if (idx >= 0)
        {
            codes.RemoveAt(idx);
            user.RecoveryCodes = protector.Protect(string.Join(";", codes));
            return Task.FromResult(true);
        }

        return Task.FromResult(false);
    }

    /// <summary>
    /// Compares two recovery codes in length-constant time to avoid leaking a
    /// per-character timing signal (#9, CWE-208).
    /// </summary>
    private static bool FixedTimeEquals(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(a),
            Encoding.UTF8.GetBytes(b)
        );

    public Task<int> CountCodesAsync(TUser user, CancellationToken cancellationToken)
    {
        var protector = dataProtectionProvider.CreateProtector(_userDataProtectionPurpose);
        var recoveryCodes = (
            user.RecoveryCodes == null ? "" : protector.Unprotect(user.RecoveryCodes)
        );

        var count = string.IsNullOrEmpty(recoveryCodes)
            ? 0
            : recoveryCodes.Split(';', StringSplitOptions.RemoveEmptyEntries).Length;
        return Task.FromResult(count);
    }

    public async Task AddOrUpdatePasskeyAsync(
        TUser user,
        UserPasskeyInfo passkey,
        CancellationToken cancellationToken
    )
    {
        var userId = UserId.Parse(user.Id);
        var credentialId = Convert.ToBase64String(passkey.CredentialId);

        var userEntity =
            await querySession
                .Query<TUser>()
                .FirstOrDefaultAsync(x => x.Id == user.Id, cancellationToken)
            ?? throw new Exception("User not found");

        var isUpdate = userEntity.Passkeys.ContainsKey(credentialId);

        if (isUpdate)
        {
            var existing = userEntity.Passkeys[credentialId].PasskeyInfo;

            // Persist counter advances. Previously a counter-only change returned
            // early and was never written, so the WebAuthn signature counter froze
            // and counter-regression clone detection was impossible (#10). Skip only
            // when nothing changed at all — including the signature counter — so an
            // advancing authenticator's counter is recorded via a PasskeyUpdated.
            if (existing.OnlyCountChanged(passkey) && existing.SignCount == passkey.SignCount)
                return;
        }

        using var session = documentStore.LightweightSession();

        session.Events.Append(
            userId.Value,
            isUpdate ? new PasskeyUpdated(userId, passkey) : new PasskeyCreated(userId, passkey)
        );

        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<IList<UserPasskeyInfo>> GetPasskeysAsync(
        TUser user,
        CancellationToken cancellationToken
    )
    {
        var userId = UserId.Parse(user.Id);

        var userEntity =
            await querySession
                .Query<TUser>()
                .FirstOrDefaultAsync(x => x.Id == user.Id, cancellationToken)
            ?? throw new Exception("User not found");

        return [.. userEntity.Passkeys.Select(kvp => kvp.Value.PasskeyInfo)];
    }

    public async Task<TUser?> FindByPasskeyIdAsync(
        byte[] credentialId,
        CancellationToken cancellationToken
    ) =>
        await querySession
            .Query<TUser>()
            .FirstOrDefaultAsync(
                x => x.Passkeys.Keys.Contains(Convert.ToBase64String(credentialId)),
                cancellationToken
            );

    public async Task<UserPasskeyInfo?> FindPasskeyAsync(
        TUser user,
        byte[] credentialId,
        CancellationToken cancellationToken
    )
    {
        var userEntity =
            await querySession
                .Query<TUser>()
                .FirstOrDefaultAsync(x => x.Id == user.Id, cancellationToken)
            ?? throw new Exception("User not found");

        return userEntity.Passkeys.TryGetValue(
            Convert.ToBase64String(credentialId),
            out var passkey
        )
            ? passkey.PasskeyInfo
            : null;
    }

    public async Task RemovePasskeyAsync(
        TUser user,
        byte[] credentialId,
        CancellationToken cancellationToken
    )
    {
        using var session = documentStore.LightweightSession();
        session.Events.Append(user.UserId.Value, new PasskeyDeleted(user.UserId, credentialId));
        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task AddToRoleAsync(
        TUser user,
        string roleName,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(roleName))
            throw new ArgumentException("Role name cannot be null or empty.", nameof(roleName));

        var role =
            await querySession
                .Query<Role>()
                .FirstOrDefaultAsync(
                    role => role.NormalizedName == roleName.ToUpperInvariant(),
                    cancellationToken
                )
            ?? throw new InvalidOperationException($"Role '{roleName}' does not exist.");

        using var session = documentStore.LightweightSession();
        session.Events.Append(
            user.StreamId,
            new RoleAssigned(
                user.UserId,
                role.RoleId,
                await currentUserService.GetCurrentUserIdAsync(cancellationToken)
            )
        );
        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveFromRoleAsync(
        TUser user,
        string roleName,
        CancellationToken cancellationToken
    )
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrWhiteSpace(roleName))
            throw new ArgumentException("Role name cannot be null or empty.", nameof(roleName));

        // Domain-layer invariant (defence in depth, independent of any UI [Authorize]):
        // the root user is the un-removable administrator anchor created during setup.
        // Stripping its Administrator role would orphan all admin access, so refuse it
        // here regardless of who initiates the call (#41).
        if (
            user.RootUser
            && string.Equals(roleName, RoleNames.Administrator, StringComparison.OrdinalIgnoreCase)
        )
        {
            throw new InvalidOperationException(
                "The root administrator cannot have the Administrator role removed."
            );
        }

        var role =
            await querySession
                .Query<Role>()
                .FirstOrDefaultAsync(
                    role => role.NormalizedName == roleName.ToUpperInvariant(),
                    cancellationToken
                )
            ?? throw new InvalidOperationException($"Role '{roleName}' does not exist.");

        using var session = documentStore.LightweightSession();
        session.Events.Append(
            user.StreamId,
            new RoleUnassigned(
                user.UserId,
                role.RoleId,
                await currentUserService.GetCurrentUserIdAsync(cancellationToken)
            )
        );
        await session.SaveChangesAsync(cancellationToken);
    }

    public async Task<IList<string>> GetRolesAsync(TUser user, CancellationToken cancellationToken)
    {
        if (user.Roles.Count == 0)
            return [];

        var streamIds = user.Roles.Select(r => r.Value).ToArray();
        var roles = await querySession
            .Query<Role>()
            .Where(r => streamIds.Contains(r.StreamId))
            .ToListAsync(cancellationToken);

        return [.. roles.Select(r => r.Name).OfType<string>()];
    }

    public async Task<bool> IsInRoleAsync(
        TUser user,
        string roleName,
        CancellationToken cancellationToken
    )
    {
        var role =
            await querySession
                .Query<Role>()
                .FirstOrDefaultAsync(
                    role => role.NormalizedName == roleName.ToUpperInvariant(),
                    cancellationToken
                )
            ?? throw new InvalidOperationException($"Role '{roleName}' does not exist.");

        return user.Roles.Any(r => r == role.RoleId);
    }

    // IUserLockoutStore

    public Task<DateTimeOffset?> GetLockoutEndDateAsync(
        TUser user,
        CancellationToken cancellationToken
    ) => Task.FromResult(user.LockoutEnd);

    public Task SetLockoutEndDateAsync(
        TUser user,
        DateTimeOffset? lockoutEnd,
        CancellationToken cancellationToken
    ) =>
        PersistLockoutStateAsync(
            user,
            current => (current.AccessFailedCount, lockoutEnd),
            cancellationToken
        );

    public async Task<int> IncrementAccessFailedCountAsync(
        TUser user,
        CancellationToken cancellationToken
    )
    {
        await PersistLockoutStateAsync(
            user,
            current => (current.AccessFailedCount + 1, current.LockoutEnd),
            cancellationToken
        );
        return user.AccessFailedCount;
    }

    public Task ResetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken) =>
        PersistLockoutStateAsync(user, current => (0, current.LockoutEnd), cancellationToken);

    /// <summary>
    /// Atomically mutates the lockout state (failed-access counter and lockout
    /// window) on the user's event stream under an exclusive stream lock.
    /// <para>
    /// ASP.NET Identity splits a failed-login into an in-memory increment followed
    /// by a separate persist, so concurrent failures would otherwise each read the
    /// same counter and write the same value (last-writer-wins), never reaching the
    /// lockout threshold (#22). <c>AppendExclusive</c> takes a transaction-scoped
    /// advisory lock on the stream; the re-read below therefore observes the latest
    /// committed counter (the inline projection commits in the same transaction),
    /// so increments serialize and accumulate.
    /// </para>
    /// </summary>
    private async Task PersistLockoutStateAsync(
        TUser user,
        Func<User, (int AccessFailedCount, DateTimeOffset? LockoutEnd)> next,
        CancellationToken cancellationToken
    )
    {
        var userId = UserId.Parse(user.Id);

        using var session = documentStore.LightweightSession();

        // Acquire the exclusive stream lock before reading current state.
        await session.Events.AppendExclusive(userId.Value);

        var current = await session.LoadAsync<User>(userId.Value, cancellationToken) ?? user;
        var (accessFailedCount, lockoutEnd) = next(current);

        user.AccessFailedCount = accessFailedCount;
        user.LockoutEnd = lockoutEnd;

        // Nothing actually changed — release the lock without churning the stream.
        if (accessFailedCount == current.AccessFailedCount && lockoutEnd == current.LockoutEnd)
            return;

        session.Events.Append(
            userId.Value,
            new UserUpdated(userId)
            {
                // Carry the current scalar state forward: the projection applies these
                // unconditionally, so omitting them would reset unrelated flags.
                EmailConfirmed = current.EmailConfirmed,
                TwoFactorEnabled = current.TwoFactorEnabled,
                Deletable = current.Deletable,
                LockoutEnabled = current.LockoutEnabled,
                LockoutEnd = lockoutEnd,
                AccessFailedCount = accessFailedCount,
            }
        );
        await session.SaveChangesAsync(cancellationToken);
    }

    public Task<int> GetAccessFailedCountAsync(TUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.AccessFailedCount);

    public Task<bool> GetLockoutEnabledAsync(TUser user, CancellationToken cancellationToken) =>
        Task.FromResult(user.LockoutEnabled);

    public Task SetLockoutEnabledAsync(
        TUser user,
        bool enabled,
        CancellationToken cancellationToken
    )
    {
        user.LockoutEnabled = enabled;
        return Task.CompletedTask;
    }

    public async Task<IList<TUser>> GetUsersInRoleAsync(
        string roleName,
        CancellationToken cancellationToken
    )
    {
        var role =
            await querySession
                .Query<Role>()
                .FirstOrDefaultAsync(
                    role => role.NormalizedName == roleName.ToUpperInvariant(),
                    cancellationToken
                )
            ?? throw new InvalidOperationException($"Role '{roleName}' does not exist.");
        var roleId = role.RoleId;

        var userIds = (
            await querySession
                .Query<UserRoleAssignment>()
                .Where(userRole => userRole.RoleGuid == roleId)
                .Select(ura => ura.UserGuid)
                .ToListAsync(cancellationToken)
        ).ToArray();

        return
        [
            .. await querySession
                .Query<TUser>()
                .Where(user => userIds.Contains(user.StreamId))
                .ToListAsync(cancellationToken),
        ];
    }
}
