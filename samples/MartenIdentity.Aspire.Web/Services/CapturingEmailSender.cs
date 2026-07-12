using System.Collections.Concurrent;
using AndreGoepel.Marten.Identity.Users;
using Microsoft.AspNetCore.Identity;

namespace MartenIdentity.Aspire.Web.Services;

/// <summary>
/// A drop-in <see cref="IEmailSender{TUser}"/> used only when the app runs under the E2E harness
/// (<c>E2E=true</c>). It behaves like <see cref="LoggingEmailSender"/> — every confirmation and
/// password-reset link is still logged — but it additionally keeps the links per recipient in
/// memory so the E2E capture endpoint (mapped in <c>Program.cs</c> under the same flag) can hand
/// them back to the test. It is never registered in a normal run, so it adds nothing to production.
/// </summary>
public sealed class CapturingEmailSender(ILogger<CapturingEmailSender> logger) : IEmailSender<User>
{
    private readonly ConcurrentDictionary<string, List<string>> _linksByEmail = new(
        StringComparer.OrdinalIgnoreCase
    );

    /// <summary>Returns every link captured for <paramref name="email"/>, oldest first.</summary>
    public IReadOnlyList<string> LinksFor(string email) =>
        _linksByEmail.TryGetValue(email, out var links) ? links.ToArray() : Array.Empty<string>();

    public Task SendConfirmationLinkAsync(User user, string email, string confirmationLink)
    {
        logger.LogInformation(
            "[Email] Confirm your account for {Email}: {ConfirmationLink}",
            email,
            confirmationLink
        );
        Capture(email, confirmationLink);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetLinkAsync(User user, string email, string resetLink)
    {
        logger.LogInformation(
            "[Email] Reset your password for {Email}: {ResetLink}",
            email,
            resetLink
        );
        Capture(email, resetLink);
        return Task.CompletedTask;
    }

    public Task SendPasswordResetCodeAsync(User user, string email, string resetCode)
    {
        logger.LogInformation(
            "[Email] Password-reset code for {Email}: {ResetCode}",
            email,
            resetCode
        );
        Capture(email, resetCode);
        return Task.CompletedTask;
    }

    private void Capture(string email, string value)
    {
        var links = _linksByEmail.GetOrAdd(email, _ => []);
        lock (links)
        {
            links.Add(value);
        }
    }
}
