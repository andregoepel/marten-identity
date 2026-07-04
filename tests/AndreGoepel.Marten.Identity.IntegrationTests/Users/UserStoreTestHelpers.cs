using AndreGoepel.Marten.Identity.Services;
using AndreGoepel.Marten.Identity.Users;
using Marten;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace AndreGoepel.Marten.Identity.IntegrationTests.Users;

internal static class UserStoreTestHelpers
{
    public static UserStore<User> BuildStore(
        IDocumentStore store,
        UserId? currentUser = null,
        IdentityOptions? identityOptions = null,
        IIdentityAuthorizer? authorizer = null
    )
    {
        var currentUserService = CurrentUserServiceFor(currentUser ?? UserId.New());

        return new UserStore<User>(
            store,
            store.QuerySession(),
            DataProtectionProvider.Create("Tests"),
            currentUserService,
            // Persistence/invariant tests default to a permissive authorizer; authz tests
            // pass a real IdentityAuthorizer to exercise the DB-authoritative check (#69).
            authorizer ?? PermissiveAuthorizer(),
            Options.Create(identityOptions ?? new IdentityOptions()),
            NullLogger<UserStore<User>>.Instance
        );
    }

    public static ICurrentUserService CurrentUserServiceFor(UserId currentUser)
    {
        var service = Substitute.For<ICurrentUserService>();
        service.GetCurrentUserIdAsync(Arg.Any<CancellationToken>()).Returns(currentUser);
        return service;
    }

    public static IIdentityAuthorizer PermissiveAuthorizer()
    {
        var authorizer = Substitute.For<IIdentityAuthorizer>();
        authorizer.IsSystemScope.Returns(true);
        authorizer.IsCurrentUserAdministratorAsync(Arg.Any<CancellationToken>()).Returns(true);
        return authorizer;
    }

    public static User NewUser(string email = "alice@example.com") =>
        new()
        {
            UserName = email,
            NormalizedUserName = email.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            PasswordHash = "hash",
        };
}
