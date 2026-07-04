using AndreGoepel.Marten.Identity.Roles;
using Microsoft.AspNetCore.Identity;

namespace AndreGoepel.Marten.Identity.Users;

public class User : IdentityUser
{
    public override string Id
    {
        get => UserId.ToString();
        set => UserId = UserId.Parse(value);
    }
    public UserId UserId { get; set; }
    public Guid StreamId
    {
        get => UserId.Value;
        set => UserId = UserId.Parse(value);
    }

    public bool RootUser { get; set; }
    public bool Deletable { get; set; } = true;
    public bool Deleted { get; set; }

    /// <summary>
    /// Optimistic-concurrency token for the user's non-lockout content (#70). Bumped by
    /// the projection on every content-changing event (create excluded) but <b>not</b> by
    /// the auto-managed lockout increments, so a stale generic update is detected while
    /// concurrent failed-login counting never triggers a spurious conflict.
    /// </summary>
    public int ContentVersion { get; set; }
    public string? AuthenticatorKey { get; set; }
    public string? RecoveryCodes { get; set; }

    // Todo: Use Hashset
    public Dictionary<string, UserPasskey> Passkeys { get; set; } = [];

    public HashSet<RoleId> Roles { get; set; } = [];

    public UserId CreatedBy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public UserId ChangedBy { get; set; }
    public DateTimeOffset ChangedAt { get; set; }
    public UserId? DeletedBy { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
}
