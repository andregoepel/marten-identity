namespace AndreGoepel.Marten.Identity.E2ETests.Infrastructure;

/// <summary>
/// App-specific page helpers on top of <see cref="AndreGoepel.Testing.E2E.PageExtensions"/>'s
/// verbatim-identical core. Selectors tied to this app's own markup (Radzen grid filtering, ...)
/// live here rather than in the shared package.
/// </summary>
public static class PageExtensions
{
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
}
