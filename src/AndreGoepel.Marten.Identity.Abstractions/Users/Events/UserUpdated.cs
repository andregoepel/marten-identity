namespace AndreGoepel.Marten.Identity.Users.Events;

public record UserUpdated(UserId UserId)
{
    public string? UserName { get; init; }
    public string? Email { get; init; }
    public string? PasswordHash { get; init; }
    public string? PhoneNumber { get; init; }
    public string? AuthenticatorKey { get; init; }
    public bool EmailConfirmed { get; init; }
    public UserId UpdatedBy { get; init; } = UserId;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
    public bool TwoFactorEnabled { get; init; }
    public string? RecoveryCodes { get; init; }
    public bool Deletable { get; init; } = true;
    public bool LockoutEnabled { get; init; }
    public DateTimeOffset? LockoutEnd { get; init; }
    public int AccessFailedCount { get; init; }

    /// <summary>
    /// Opaque security stamp. When it changes, previously issued authentication
    /// cookies stop revalidating, signing the user out everywhere.
    /// </summary>
    public string? SecurityStamp { get; init; }

    /// <summary>
    /// True when this update only carries auto-managed lockout state (failed-count /
    /// lockout window) and no user-visible content change. The projection skips bumping
    /// <see cref="Users.User.ContentVersion" /> for these, so lockout increments do not
    /// trigger optimistic-concurrency conflicts on the generic update path (#70).
    /// </summary>
    public bool LockoutOnly { get; init; }
}
