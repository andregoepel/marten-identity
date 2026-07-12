using System.Net;
using System.Text.Json;

namespace AndreGoepel.Marten.Identity.E2ETests.Infrastructure;

/// <summary>
/// Reads the confirmation/reset links the sample captured, via its E2E-only <c>/e2e/emails</c>
/// endpoint (enabled by <c>E2E=true</c>). The sample sends no real email, so this stands in for the
/// mail-server client a MailHog/SMTP setup would use — but without an extra container.
/// </summary>
public sealed class CapturedEmailClient(string appBaseUrl) : IDisposable
{
    private readonly HttpClient _http = new(
        new HttpClientHandler
        {
            // The app is served over HTTPS with the ASP.NET Core dev cert.
            ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator,
        }
    )
    {
        BaseAddress = new Uri(appBaseUrl),
    };

    /// <summary>
    /// No-op kept for symmetry with a mail-inbox client: each test uses a unique email address and
    /// filters links by path fragment, so there is no shared inbox state to clear.
    /// </summary>
    public Task ClearAsync() => Task.CompletedTask;

    /// <summary>
    /// Polls the capture endpoint until a link for <paramref name="toEmail"/> containing
    /// <paramref name="mustContain"/> (e.g. <c>Account/ConfirmEmail</c> or <c>Account/ResetPassword</c>)
    /// appears, then returns it (HTML-decoded so query separators are usable).
    /// </summary>
    public async Task<string> WaitForLinkAsync(
        string toEmail,
        string mustContain,
        TimeSpan? timeout = null,
        CancellationToken ct = default
    )
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(30));

        while (DateTime.UtcNow < deadline)
        {
            var link = await TryFindLinkAsync(toEmail, mustContain, ct);
            if (link is not null)
            {
                return link;
            }

            await Task.Delay(250, ct);
        }

        throw new TimeoutException(
            $"No captured email link containing '{mustContain}' for '{toEmail}' appeared within the timeout."
        );
    }

    private async Task<string?> TryFindLinkAsync(
        string toEmail,
        string mustContain,
        CancellationToken ct
    )
    {
        using var response = await _http.GetAsync(
            "e2e/emails?email=" + Uri.EscapeDataString(toEmail),
            ct
        );
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var json = await response.Content.ReadAsStringAsync(ct);
        var links = JsonSerializer.Deserialize<string[]>(json) ?? [];

        // Newest first: a resent link supersedes an older one for the same recipient.
        for (var i = links.Length - 1; i >= 0; i--)
        {
            var link = WebUtility.HtmlDecode(links[i]);
            if (link.Contains(mustContain, StringComparison.OrdinalIgnoreCase))
            {
                return link;
            }
        }

        return null;
    }

    public void Dispose() => _http.Dispose();
}
