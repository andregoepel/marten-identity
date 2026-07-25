namespace AndreGoepel.Marten.Identity.Services;

/// <summary>
/// Thrown only by <c>UserStore</c>'s explicit <c>IUserRoleStore</c> implementation, which
/// adapts a failed role-assignment <see cref="Microsoft.AspNetCore.Identity.IdentityResult" />
/// back into a throw for hosts calling through <c>UserManager</c> — that interface returns a
/// plain <c>Task</c> and has no channel to carry a result (#69/#41). Callers that use
/// <c>UserStore</c>'s own <c>AddToRoleAsync</c>/<c>RemoveFromRoleAsync</c> overloads directly
/// get the failed <see cref="Microsoft.AspNetCore.Identity.IdentityResult" /> instead and
/// never see this exception.
/// </summary>
public sealed class IdentityAuthorizationException(string message) : Exception(message);
