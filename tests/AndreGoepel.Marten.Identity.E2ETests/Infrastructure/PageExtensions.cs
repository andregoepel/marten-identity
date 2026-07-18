namespace AndreGoepel.Marten.Identity.E2ETests.Infrastructure;

/// <summary>
/// Helpers that hide the Blazor-Server timing and Radzen markup details from individual tests, so a
/// test reads as intent ("fill Email, click Log in") rather than selector plumbing.
/// </summary>
public static class PageExtensions
{
    /// <summary>
    /// Waits until the interactive Server circuit is live. Radzen forms submit through Blazor event
    /// handlers, so clicking before the circuit connects silently does nothing — this prevents flakes.
    /// </summary>
    public static async Task WaitForBlazorAsync(this IPage page)
    {
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.WaitForFunctionAsync(
            "() => window.Blazor !== undefined && !document.querySelector('#components-reconnect-modal.components-reconnect-show')"
        );
    }

    /// <summary>Navigates to a relative path (resolved against the fixture base URL) and waits for interactivity.</summary>
    public static async Task GotoAsync(this IPage page, string path)
    {
        await page.GotoAsync(path);
        await page.WaitForBlazorAsync();
    }

    /// <summary>Fills a Radzen input rendered with <c>Name="..."</c>.</summary>
    public static Task FillFieldAsync(this IPage page, string name, string value) =>
        page.FillAsync($"[name='{name}']", value);

    /// <summary>Clicks a Radzen button by its visible text.</summary>
    public static Task ClickButtonAsync(this IPage page, string text) =>
        page.GetByRole(AriaRole.Button, new() { Name = text, Exact = false }).First.ClickAsync();

    /// <summary>Clicks a link by its visible text.</summary>
    public static Task ClickLinkAsync(this IPage page, string text) =>
        page.GetByRole(AriaRole.Link, new() { Name = text, Exact = false }).First.ClickAsync();

    /// <summary>
    /// Types <paramref name="term"/> into the admin users-grid filter and waits until the grid has
    /// actually narrowed to it. The grid filters over the Blazor-Server circuit (an <c>@oninput</c>
    /// handler that reloads the grid), so an input event landing before that handler attaches is
    /// silently dropped — the same connect gap the login and setup flows guard against. Without the
    /// filter the freshly-created row sits on a later page of the 10-row grid and never renders, so
    /// retry the fill until the matching row is on screen.
    /// </summary>
    public static async Task FilterUsersGridAsync(this IPage page, string term)
    {
        var input = page.Locator(".ag-search-input");
        var row = page.Locator(".rz-data-grid tr", new() { HasTextString = term });

        for (var attempt = 0; ; attempt++)
        {
            await input.FillAsync(term);
            try
            {
                await row.First.WaitForAsync(
                    new LocatorWaitForOptions
                    {
                        State = WaitForSelectorState.Visible,
                        Timeout = 3_000,
                    }
                );
                return;
            }
            catch (TimeoutException) when (attempt < 5)
            {
                // Input event dropped before the circuit's handler attached — type it again.
            }
        }
    }

    /// <summary>Asserts the current URL path matches (ignoring query string and trailing slash).</summary>
    public static async Task AssertOnPathAsync(this IPage page, string expectedPath)
    {
        try
        {
            await page.WaitForURLAsync(
                url =>
                    NormalizePath(url)
                        .Contains(expectedPath.Trim('/'), StringComparison.OrdinalIgnoreCase),
                new PageWaitForURLOptions { Timeout = 15_000 }
            );
        }
        catch (TimeoutException)
        {
            throw new TimeoutException(
                $"Expected path to contain '{expectedPath}' but was '{page.Url}'."
            );
        }
    }

    private static string NormalizePath(string url) => new Uri(url).AbsolutePath.Trim('/');
}
