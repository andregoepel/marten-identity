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
        IdentityOptions? identityOptions = null
    )
    {
        var currentUserService = Substitute.For<ICurrentUserService>();
        currentUserService
            .GetCurrentUserIdAsync(Arg.Any<CancellationToken>())
            .Returns(currentUser ?? UserId.New());

        return new UserStore<User>(
            store,
            store.QuerySession(),
            DataProtectionProvider.Create("Tests"),
            currentUserService,
            Options.Create(identityOptions ?? new IdentityOptions()),
            NullLogger<UserStore<User>>.Instance
        );
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
