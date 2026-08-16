namespace AndreGoepel.Marten.Identity.Users.Events;

/// <summary>
/// Legacy full-state update event. The store stopped writing this in v2.0.0 (#138) in
/// favor of fine-grained events (<c>EmailChanged</c>, <c>PasswordChanged</c>,
/// <c>SecurityStampRotated</c>, and others in this namespace) — one per changed field
/// instead of the whole user snapshot. This type stays public and is still dispatched by
/// the projection so that streams written before the upgrade keep replaying correctly; it
/// is never appended by new writes.
/// </summary>
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
    /// Legacy: the store no longer writes this — lockout state has its own events
    /// (<c>LockedOut</c>, <c>LockoutCleared</c>, <c>AccessFailedCountChanged</c>) since
    /// v2.0.0 (#138), which the projection likewise never bumps ContentVersion for.
    /// </summary>
    public bool LockoutOnly { get; init; }
}
