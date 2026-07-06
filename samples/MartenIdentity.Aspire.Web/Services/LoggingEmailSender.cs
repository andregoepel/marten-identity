using AndreGoepel.Marten.Identity.Users;
using Microsoft.AspNetCore.Identity;

namespace MartenIdentity.Aspire.Web.Services;

/// <summary>
/// A development <see cref="IEmailSender{TUser}"/> that logs the messages the Identity UI
/// would otherwise email (confirmation links, password-reset links and codes) instead of
/// delivering them. Watch the "web" resource logs in the Aspire dashboard to grab the link.
/// Replace this with a real transactional-email integration before deploying.
/// </summary>
public sealed class LoggingEmailSender(ILogger<LoggingEmailSender> logger) : IEmailSender<User>
{
    public Task SendConfirmationLinkAsync(User user, string email, string confirmationLink)
    {
        logger.LogInformation(
            "[Email] Confirm your account for {Email}: {ConfirmationLink}",
            email,
            confirmationLink
        );
        return Task.CompletedTask;
    }

    public Task SendPasswordResetLinkAsync(User user, string email, string resetLink)
    {
        logger.LogInformation(
            "[Email] Reset your password for {Email}: {ResetLink}",
            email,
            resetLink
        );
        return Task.CompletedTask;
    }

    public Task SendPasswordResetCodeAsync(User user, string email, string resetCode)
    {
        logger.LogInformation(
            "[Email] Password-reset code for {Email}: {ResetCode}",
            email,
            resetCode
        );
        return Task.CompletedTask;
    }
}
