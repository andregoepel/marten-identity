namespace AndreGoepel.Marten.Identity.Services;

/// <summary>
/// Thrown when a privileged identity store operation that returns no result — the
/// role-assignment methods (<c>IUserRoleStore</c>) — is invoked without the required
/// authorization (#69/#41). Operations that return
/// <see cref="Microsoft.AspNetCore.Identity.IdentityResult" /> surface the same condition
/// as a failed result instead.
/// </summary>
public sealed class IdentityAuthorizationException(string message) : Exception(message);
