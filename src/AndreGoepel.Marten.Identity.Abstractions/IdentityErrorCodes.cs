namespace AndreGoepel.Marten.Identity;

/// <summary>
/// Stable, culture-invariant codes for the <see cref="Microsoft.AspNetCore.Identity.IdentityError"/>s
/// the Marten-backed stores and services raise (#114).
/// </summary>
/// <remarks>
/// <para>
/// The <c>Description</c> on each error stays English and remains a usable fallback, so nothing
/// that reads it today breaks. But English text is not a contract: a UI that wants to show these
/// errors in the visitor's language matches on the <c>Code</c> and looks up its own translation.
/// Matching on <c>Description</c> would break the moment the wording is edited.
/// </para>
/// <para>
/// Treat these values as API: they are persisted in no store, but consuming apps branch on them,
/// so renaming one is a breaking change.
/// </para>
/// </remarks>
public static class IdentityErrorCodes
{
    /// <summary>The caller lacks administrator authority, or is unidentified entirely.</summary>
    /// <remarks>
    /// The description carries extra diagnostic context that is <b>not</b> derivable from this
    /// code: when the caller is unidentified rather than merely non-admin, it names the
    /// <c>BeginSystemScope</c> escape hatch (#101). A translated message should keep that
    /// distinction rather than collapsing both cases into one sentence.
    /// </remarks>
    public const string NotAuthorized = nameof(NotAuthorized);

    /// <summary>The document was modified by someone else between read and write.</summary>
    public const string ConcurrencyFailure = nameof(ConcurrencyFailure);

    /// <summary>A root user already exists; only one is permitted (#41).</summary>
    public const string RootUserAlreadyExists = nameof(RootUserAlreadyExists);

    /// <summary>An account already exists for the invited email address.</summary>
    /// <remarks>The description embeds the address; a translation should take it as an argument.</remarks>
    public const string DuplicateEmail = nameof(DuplicateEmail);

    /// <summary>The invitation being resent has already been accepted.</summary>
    public const string InvitationAlreadyAccepted = nameof(InvitationAlreadyAccepted);

    /// <summary>Persisting the user failed.</summary>
    public const string UserSaveFailed = nameof(UserSaveFailed);

    /// <summary>The user is protected from deletion (e.g. the root user).</summary>
    public const string UserNotDeletable = nameof(UserNotDeletable);

    /// <summary>Deleting the user failed.</summary>
    public const string UserDeleteFailed = nameof(UserDeleteFailed);

    /// <summary>Restoring the soft-deleted user failed.</summary>
    public const string UserRestoreFailed = nameof(UserRestoreFailed);

    /// <summary>A role name is required but was null or empty.</summary>
    public const string RoleNameRequired = nameof(RoleNameRequired);

    /// <summary>Persisting the role failed.</summary>
    public const string RoleSaveFailed = nameof(RoleSaveFailed);

    /// <summary>The role is protected from deletion.</summary>
    public const string RoleNotDeletable = nameof(RoleNotDeletable);

    /// <summary>Deleting the role failed.</summary>
    public const string RoleDeleteFailed = nameof(RoleDeleteFailed);

    /// <summary>Restoring the soft-deleted role failed.</summary>
    public const string RoleRestoreFailed = nameof(RoleRestoreFailed);
}
