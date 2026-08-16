using System.Diagnostics.CodeAnalysis;
using AndreGoepel.Marten.Identity.Users.Events;
using Marten.Events.Aggregation;

namespace AndreGoepel.Marten.Identity.Users;

internal sealed partial class UserProjection : SingleStreamProjection<User, Guid>
{
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Called by Marten source-generated dispatcher"
    )]
    public void Apply(UserCreated @event, User user)
    {
        user.UserId = @event.UserId;
        user.UserName = @event.UserName;
        user.NormalizedUserName = @event.UserName?.ToUpperInvariant();
        user.Email = @event.Email;
        user.NormalizedEmail = @event.Email?.ToUpperInvariant();
        user.PasswordHash = @event.PasswordHash;
        user.Deletable = @event.Deletable;
        user.RootUser = @event.RootUser;
        user.EmailConfirmed = @event.EmailConfirmed;
        user.LockoutEnabled = @event.LockoutEnabled;
        user.SecurityStamp = @event.SecurityStamp;
        user.CreatedBy = @event.CreatedBy;
        user.CreatedAt = @event.CreatedAt;
        user.ChangedBy = @event.CreatedBy;
        user.ChangedAt = @event.CreatedAt;
    }

    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Called by Marten source-generated dispatcher"
    )]
    public void Apply(UserDeleted @event, User user)
    {
        user.UserName = null;
        user.NormalizedUserName = null;
        user.Email = null;
        user.NormalizedEmail = null;
        user.PasswordHash = null;
        user.Deleted = true;
        user.DeletedBy = @event.DeletedBy;
        user.DeletedAt = @event.DeletedAt;
    }

    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Called by Marten source-generated dispatcher"
    )]
    public void Apply(UserRestored @event, User user)
    {
        user.Deleted = false;
        user.DeletedBy = null;
        user.DeletedAt = null;

        if (@event.UserName is not null)
        {
            user.UserName = @event.UserName;
            user.NormalizedUserName = @event.UserName.ToUpperInvariant();
        }

        if (@event.Email is not null)
        {
            user.Email = @event.Email;
            user.NormalizedEmail = @event.Email.ToUpperInvariant();
        }

        if (@event.PasswordHash is not null)
            user.PasswordHash = @event.PasswordHash;

        if (@event.SecurityStamp is not null)
            user.SecurityStamp = @event.SecurityStamp;

        // Restoring changes content — advance the optimistic-concurrency token (#70).
        user.ContentVersion++;
    }

    // Legacy snapshot event — replay only; the store emits fine-grained events since v2.0.0 (#138).
    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Called by Marten source-generated dispatcher"
    )]
    public void Apply(UserUpdated @event, User user)
    {
        if (@event.UserName is not null)
        {
            user.UserName = @event.UserName;
            user.NormalizedUserName = @event.UserName?.ToUpperInvariant();
        }

        user.EmailConfirmed = @event.EmailConfirmed;
        if (@event.Email is not null)
        {
            user.Email = @event.Email;
            user.NormalizedEmail = @event.Email?.ToUpperInvariant();
        }

        if (@event.PhoneNumber is not null)
            user.PhoneNumber = @event.PhoneNumber;

        if (@event.PasswordHash is not null)
            user.PasswordHash = @event.PasswordHash;

        if (@event.SecurityStamp is not null)
            user.SecurityStamp = @event.SecurityStamp;

        #region TwoFactor Authentication

        if (@event.AuthenticatorKey is not null)
            user.AuthenticatorKey = @event.AuthenticatorKey;

        if (@event.RecoveryCodes is not null)
            user.RecoveryCodes = @event.RecoveryCodes;

        user.TwoFactorEnabled = @event.TwoFactorEnabled;

        #endregion TwoFactor Authentication

        user.Deletable = @event.Deletable;

        user.LockoutEnabled = @event.LockoutEnabled;
        user.LockoutEnd = @event.LockoutEnd;
        user.AccessFailedCount = @event.AccessFailedCount;

        user.ChangedBy = @event.UpdatedBy;
        user.ChangedAt = @event.UpdatedAt;

        // Advance the optimistic-concurrency token for genuine content changes only;
        // lockout-only updates (failed-count / lockout window) must not bump it, or
        // concurrent failed-login counting would spuriously conflict with a profile
        // update (#70).
        if (!@event.LockoutOnly)
            user.ContentVersion++;
    }

    #region Fine-grained events (#138)

    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Called by Marten source-generated dispatcher"
    )]
    public void Apply(EmailChanged @event, User user)
    {
        // null = masked (#67) or unchanged — never overwrite.
        if (@event.Email is not null)
        {
            user.Email = @event.Email;
            user.NormalizedEmail = @event.Email.ToUpperInvariant();
        }
        user.EmailConfirmed = @event.EmailConfirmed;
        Touch(user, @event);
        user.ContentVersion++;
    }

    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Called by Marten source-generated dispatcher"
    )]
    public void Apply(EmailConfirmationChanged @event, User user)
    {
        user.EmailConfirmed = @event.EmailConfirmed;
        Touch(user, @event);
        user.ContentVersion++;
    }

    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Called by Marten source-generated dispatcher"
    )]
    public void Apply(UserNameChanged @event, User user)
    {
        if (@event.UserName is not null)
        {
            user.UserName = @event.UserName;
            user.NormalizedUserName = @event.UserName.ToUpperInvariant();
        }
        Touch(user, @event);
        user.ContentVersion++;
    }

    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Called by Marten source-generated dispatcher"
    )]
    public void Apply(PhoneNumberChanged @event, User user)
    {
        if (@event.PhoneNumber is not null)
            user.PhoneNumber = @event.PhoneNumber;
        Touch(user, @event);
        user.ContentVersion++;
    }

    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Called by Marten source-generated dispatcher"
    )]
    public void Apply(PasswordChanged @event, User user)
    {
        if (@event.PasswordHash is not null)
            user.PasswordHash = @event.PasswordHash;
        Touch(user, @event);
        user.ContentVersion++;
    }

    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Called by Marten source-generated dispatcher"
    )]
    public void Apply(SecurityStampRotated @event, User user)
    {
        if (@event.SecurityStamp is not null)
            user.SecurityStamp = @event.SecurityStamp;
        Touch(user, @event);
        user.ContentVersion++;
    }

    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Called by Marten source-generated dispatcher"
    )]
    public void Apply(TwoFactorChanged @event, User user)
    {
        if (@event.AuthenticatorKey is not null)
            user.AuthenticatorKey = @event.AuthenticatorKey;
        if (@event.RecoveryCodes is not null)
            user.RecoveryCodes = @event.RecoveryCodes;
        user.TwoFactorEnabled = @event.TwoFactorEnabled;
        Touch(user, @event);
        user.ContentVersion++;
    }

    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Called by Marten source-generated dispatcher"
    )]
    public void Apply(DeletabilityChanged @event, User user)
    {
        user.Deletable = @event.Deletable;
        Touch(user, @event);
        user.ContentVersion++;
    }

    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Called by Marten source-generated dispatcher"
    )]
    public void Apply(LockoutEnablementChanged @event, User user)
    {
        user.LockoutEnabled = @event.LockoutEnabled;
        Touch(user, @event);
        // A deliberate admin/policy decision, not an auto-managed counter — treated as
        // content, unlike the three lockout events below.
        user.ContentVersion++;
    }

    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Called by Marten source-generated dispatcher"
    )]
    public void Apply(LockedOut @event, User user)
    {
        user.LockoutEnd = @event.LockoutEnd;
        Touch(user, @event);
        // Auto-managed lockout state must not bump ContentVersion, or concurrent failed-login
        // counting would spuriously conflict with an unrelated profile update (#70).
    }

    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Called by Marten source-generated dispatcher"
    )]
    public void Apply(LockoutCleared @event, User user)
    {
        user.LockoutEnd = null;
        Touch(user, @event);
        // See Apply(LockedOut, ...) above — no ContentVersion bump.
    }

    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Called by Marten source-generated dispatcher"
    )]
    public void Apply(AccessFailedCountChanged @event, User user)
    {
        user.AccessFailedCount = @event.AccessFailedCount;
        Touch(user, @event);
        // See Apply(LockedOut, ...) above — no ContentVersion bump.
    }

    /// <summary>Shared audit stamp for every fine-grained user event (§2.1, #138).</summary>
    private static void Touch(User user, IUserAuditedEvent @event)
    {
        user.ChangedBy = @event.ChangedBy;
        user.ChangedAt = @event.ChangedAt;
    }

    #endregion Fine-grained events (#138)

    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Called by Marten source-generated dispatcher"
    )]
    public void Apply(PasskeyCreated @event, User user)
    {
        // Masked events carry a null payload; skip so a rebuild over an erased stream stays safe (#67).
        if (@event.Passkey is null)
            return;

        var passkeyInfo = new UserPasskey { PasskeyInfo = @event.Passkey };
        user.Passkeys[passkeyInfo.CredentialId] = passkeyInfo;
    }

    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Called by Marten source-generated dispatcher"
    )]
    public void Apply(PasskeyUpdated @event, User user)
    {
        // Masked events carry a null payload; skip so a rebuild over an erased stream stays safe (#67).
        if (@event.Passkey is null)
            return;

        var passkeyInfo = new UserPasskey { PasskeyInfo = @event.Passkey };
        user.Passkeys[passkeyInfo.CredentialId] = passkeyInfo;
    }

    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Called by Marten source-generated dispatcher"
    )]
    public void Apply(PasskeyDeleted @event, User user)
    {
        user.Passkeys.Remove(Convert.ToBase64String(@event.CredentialId));
    }

    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Called by Marten source-generated dispatcher"
    )]
    public void Apply(RoleAssigned @event, User user)
    {
        user.Roles.Add(@event.RoleId);
        user.ChangedAt = @event.AssignedAt;
        user.ChangedBy = @event.AssignedBy;
    }

    [SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "Called by Marten source-generated dispatcher"
    )]
    public void Apply(RoleUnassigned @event, User user)
    {
        user.Roles.RemoveWhere(role => role == @event.RoleId);
        user.ChangedAt = @event.UnassignedAt;
        user.ChangedBy = @event.UnassignedBy;
    }
}
