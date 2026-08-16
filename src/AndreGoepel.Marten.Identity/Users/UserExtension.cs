namespace AndreGoepel.Marten.Identity.Users;

/// <summary>
/// Per-field diff between two <see cref="User" /> snapshots, one flag per persisted field
/// (#138). Drives which fine-grained event(s) <see cref="UserStore{TUser}.UpdateAsync" />
/// appends instead of a single full-state <c>UserUpdated</c>.
/// </summary>
internal readonly record struct UserChangeSet(
    bool UserName,
    bool Email,
    bool EmailConfirmed,
    bool PhoneNumber,
    bool PasswordHash,
    bool SecurityStamp,
    bool AuthenticatorKey,
    bool RecoveryCodes,
    bool TwoFactorEnabled,
    bool Deletable,
    bool LockoutEnabled,
    bool LockoutEnd,
    bool AccessFailedCount
)
{
    public bool Any =>
        UserName
        || Email
        || EmailConfirmed
        || PhoneNumber
        || PasswordHash
        || SecurityStamp
        || AuthenticatorKey
        || RecoveryCodes
        || TwoFactorEnabled
        || Deletable
        || LockoutEnabled
        || LockoutEnd
        || AccessFailedCount;
}

internal static class UserExtension
{
    public static UserChangeSet DiffAgainst(this User @this, User other) =>
        new(
            UserName: @this.UserName != other.UserName,
            Email: @this.Email != other.Email,
            EmailConfirmed: @this.EmailConfirmed != other.EmailConfirmed,
            PhoneNumber: @this.PhoneNumber != other.PhoneNumber,
            PasswordHash: @this.PasswordHash != other.PasswordHash,
            SecurityStamp: @this.SecurityStamp != other.SecurityStamp,
            AuthenticatorKey: @this.AuthenticatorKey != other.AuthenticatorKey,
            RecoveryCodes: @this.RecoveryCodes != other.RecoveryCodes,
            TwoFactorEnabled: @this.TwoFactorEnabled != other.TwoFactorEnabled,
            Deletable: @this.Deletable != other.Deletable,
            LockoutEnabled: @this.LockoutEnabled != other.LockoutEnabled,
            LockoutEnd: @this.LockoutEnd != other.LockoutEnd,
            AccessFailedCount: @this.AccessFailedCount != other.AccessFailedCount
        );
}
