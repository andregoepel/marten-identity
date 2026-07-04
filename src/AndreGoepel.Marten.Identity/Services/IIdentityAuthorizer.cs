namespace AndreGoepel.Marten.Identity.Services;

/// <summary>
/// Domain-layer authorization for privileged identity store operations (#69/#41).
/// <para>
/// The stores are a reusable persistence layer; the page-level <c>[Authorize]</c>
/// attributes are the primary guard, but they are a single point of failure — a
/// forgotten attribute, a permissive host fallback policy, or any caller reaching a
/// store directly becomes a privilege-escalation path. This service is the
/// defence-in-depth backstop: privileged operations re-check authorization here,
/// independent of the UI.
/// </para>
/// <para>
/// It also provides an explicit escape hatch — <see cref="BeginSystemScope"/> — for
/// trusted server-side code (seeding, background provisioning, an alternative first-run
/// bootstrap) that legitimately acts without an authenticated administrator.
/// </para>
/// </summary>
public interface IIdentityAuthorizer
{
    /// <summary>
    /// Enters a trusted scope in which the domain authorization checks are bypassed. The
    /// scope is ambient (it flows across awaits within the same execution context) and
    /// ends when the returned handle is disposed. Use only from trusted server-side code
    /// you control — never in response to untrusted input.
    /// </summary>
    IDisposable BeginSystemScope();

    /// <summary>True while executing inside a <see cref="BeginSystemScope"/>.</summary>
    bool IsSystemScope { get; }

    /// <summary>
    /// True when the operation may act with administrator authority: inside a system
    /// scope, or when the current user (from <see cref="ICurrentUserService"/>) holds the
    /// non-deleted Administrator role. Fails closed — returns <c>false</c> — when there is
    /// no identified current user.
    /// </summary>
    Task<bool> IsCurrentUserAdministratorAsync(CancellationToken cancellationToken = default);
}
