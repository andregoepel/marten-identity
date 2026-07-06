using AndreGoepel.Marten.Identity.Blazor.Components.Account.Pages;
using AndreGoepel.Marten.Identity.Blazor.Features;
using AndreGoepel.Marten.Identity.Http;
using AndreGoepel.Marten.Identity.Users;
using Bunit;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Radzen;

namespace AndreGoepel.Marten.Identity.Blazor.Tests.Account.Pages;

public class LoginTests : BunitContext
{
    private const string RegisterLink = "Create account";
    private const string PasskeyButton = "Log in with a passkey";

    [Fact]
    public void AllEnabled_ShowsRegisterLinkAndPasskeyButton()
    {
        var cut = Render(new IdentityFeatureFlags());

        Assert.Contains(RegisterLink, cut.Markup);
        Assert.Contains(PasskeyButton, cut.Markup);
    }

    [Fact]
    public void RegistrationDisabled_HidesRegisterLink()
    {
        var cut = Render(new IdentityFeatureFlags { UserRegistration = false });

        Assert.DoesNotContain(RegisterLink, cut.Markup);
        Assert.Contains(PasskeyButton, cut.Markup); // unrelated feature untouched
    }

    [Fact]
    public void PasskeyDisabled_HidesPasskeyButton()
    {
        var cut = Render(new IdentityFeatureFlags { Passkey = false });

        Assert.DoesNotContain(PasskeyButton, cut.Markup);
        Assert.Contains(RegisterLink, cut.Markup);
    }

    private IRenderedComponent<Login> Render(IdentityFeatureFlags flags)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;

        var um = Substitute.For<UserManager<User>>(
            Substitute.For<IUserStore<User>>(),
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null
        );
        var sm = Substitute.For<SignInManager<User>>(
            um,
            Substitute.For<IHttpContextAccessor>(),
            Substitute.For<IUserClaimsPrincipalFactory<User>>(),
            Options.Create(new IdentityOptions()),
            Substitute.For<ILogger<SignInManager<User>>>(),
            Substitute.For<IAuthenticationSchemeProvider>(),
            Substitute.For<IUserConfirmation<User>>()
        );

        Services.AddSingleton(um);
        Services.AddSingleton(sm);
        Services.AddSingleton<IPasswordHasher<User>>(new PasswordHasher<User>());
        Services.AddSingleton(Substitute.For<ILogger<Login>>());
        Services.AddSingleton(new NotificationService());
        Services.AddSingleton(new LoginTokenProtector(DataProtectionProvider.Create("Tests")));
        Services.AddSingleton<IIdentityFeatureProvider>(new StubProvider(flags));

        return Render<Login>();
    }

    private sealed class StubProvider(IdentityFeatureFlags flags) : IIdentityFeatureProvider
    {
        public ValueTask<IdentityFeatureFlags> GetAsync(
            CancellationToken cancellationToken = default
        ) => ValueTask.FromResult(flags);
    }
}
