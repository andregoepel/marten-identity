namespace AndreGoepel.Marten.Identity.Users.Events;

public record UserCreated(UserId UserId, string? UserName, string? Email, string? PasswordHash)
{
    public bool RootUser { get; init; }
    public bool Deletable { get; init; } = true;
    public bool EmailConfirmed { get; init; }

    /// <summary>
    /// Whether the account participates in lockout. Defaults to <c>true</c> so that
    /// brute-force protection is active out of the box — including for events written
    /// before this field existed, which deserialize to the default. See
    /// <see cref="UserId" />-keyed users created via the store.
    /// </summary>
    public bool LockoutEnabled { get; init; } = true;

    /// <summary>
    /// Opaque value that changes whenever security-sensitive data (password, 2FA,
    /// logins) changes. Used to invalidate previously issued authentication cookies.
    /// </summary>
    public string? SecurityStamp { get; init; }

    public UserId CreatedBy { get; init; } = UserId;
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
