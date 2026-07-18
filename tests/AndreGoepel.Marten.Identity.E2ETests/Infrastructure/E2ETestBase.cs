namespace AndreGoepel.Marten.Identity.E2ETests.Infrastructure;

/// <summary>
/// Base class for every E2E test: one fresh browser context + page per test (so cookies never leak
/// between tests) plus the common account flows expressed as intent-revealing helpers.
/// </summary>
[Collection(E2ECollection.Name)]
public abstract class E2ETestBase(E2EAppFixture fixture) : IAsyncLifetime
{
    protected E2EAppFixture Fixture { get; } = fixture;
    protected IBrowserContext Context { get; private set; } = default!;
    protected IPage Page { get; private set; } = default!;

    public async ValueTask InitializeAsync()
    {
        Context = await Fixture.NewContextAsync();

        // Record a Playwright trace for the whole test. It is only written to disk when the
        // test fails (see DisposeAsync) — CI then uploads it as an artifact, which is the
        // only way to see what the page actually looked like on a headless runner.
        await Context.Tracing.StartAsync(
            new TracingStartOptions
            {
                Screenshots = true,
                Snapshots = true,
                Sources = true,
            }
        );

        Page = await Context.NewPageAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await StopTracingAsync();
        await Context.DisposeAsync();
    }

    /// <summary>
    /// Saves the trace only for a failed test (kept small: passing runs discard theirs). The file
    /// lands in PLAYWRIGHT_TRACE_DIR — set by CI so the upload step can find it — named after the
    /// test so a multi-failure run yields one openable trace per failure.
    /// </summary>
    private async Task StopTracingAsync()
    {
        var failed = TestContext.Current.TestState?.Result == TestResult.Failed;
        if (!failed)
        {
            await Context.Tracing.StopAsync();
            return;
        }

        var dir = Environment.GetEnvironmentVariable("PLAYWRIGHT_TRACE_DIR");
        dir = string.IsNullOrWhiteSpace(dir) ? "playwright-traces" : dir;
        Directory.CreateDirectory(dir);

        var name = SanitizeFileName(TestContext.Current.Test?.TestDisplayName ?? "e2e-trace");
        await Context.Tracing.StopAsync(
            new TracingStopOptions { Path = Path.Combine(dir, $"{name}.zip") }
        );
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return name;
    }

    #region Account flows

    /// <summary>Logs the current page's session in via the real cookie-login flow.</summary>
    protected async Task LoginAsync(string email, string password, IPage? page = null)
    {
        page ??= Page;
        await page.GotoAsync("/Account/Login");
        await page.WaitForBlazorAsync();
        await page.FillFieldAsync("Email", email);
        await page.FillFieldAsync("Password", password);
        await ClickAndLeaveLoginAsync(page);
    }

    /// <summary>
    /// Clicks "Log in" and waits to leave the login page. A click can land in the gap between the
    /// circuit connecting and the Radzen form's submit handler attaching — it is then silently lost —
    /// so the click is retried until the cookie middleware redirects away. Exact-path equality keeps a
    /// redirect to /Account/LoginWith2fa (which *contains* /Account/Login) counting as "left".
    /// </summary>
    private static async Task ClickAndLeaveLoginAsync(IPage page)
    {
        for (var attempt = 0; ; attempt++)
        {
            await page.ClickButtonAsync("Log in");
            try
            {
                await page.WaitForURLAsync(
                    url =>
                        !new Uri(url).AbsolutePath.Equals(
                            "/Account/Login",
                            StringComparison.OrdinalIgnoreCase
                        ),
                    new PageWaitForURLOptions { Timeout = 5_000 }
                );
                return;
            }
            catch (TimeoutException) when (attempt < 5)
            {
                // Submit was lost before the handler attached — click again.
            }
        }
    }

    /// <summary>Ensures the root admin exists, then logs this page in as that admin.</summary>
    protected async Task LoginAsAdminAsync(IPage? page = null)
    {
        await Fixture.ProvisionAdminAsync();
        await LoginAsync(TestData.AdminEmail, TestData.DefaultPassword, page);
    }

    /// <summary>Registers a new user and returns the generated email; the account still needs confirmation.</summary>
    protected async Task<string> RegisterAsync(string? email = null, string? password = null)
    {
        email ??= TestData.NewEmail();
        password ??= TestData.DefaultPassword;

        await Page.GotoAsync("/Account/Register");
        await Page.WaitForBlazorAsync();
        await Page.FillFieldAsync("Email", email);
        await Page.FillFieldAsync("NewPassword", password);
        await Page.FillFieldAsync("ConfirmPassword", password);
        await Page.ClickButtonAsync("Register");
        return email;
    }

    /// <summary>Reads the confirmation link the sample logged and follows it to activate the account.</summary>
    protected async Task ConfirmEmailAsync(string email)
    {
        var link = await Fixture.Email.WaitForLinkAsync(email, "Account/ConfirmEmail");
        await Page.GotoAsync(link);
        await Page.WaitForBlazorAsync();
    }

    /// <summary>Signs the current session out through the app's sign-out endpoint.</summary>
    protected async Task LogoutAsync(IPage? page = null)
    {
        page ??= Page;
        await page.GotoAsync("/Account/SignOutAndRedirect");
    }

    #endregion
}
