using Cafe.Api.Services.Interfaces;
namespace Cafe.Api.Infrastructure.Mail;

/// <summary>Used when no email provider is configured: it records the email in the log instead of sending it.</summary>
public sealed class LogEmailSender(ILogger<LogEmailSender> logger) : IEmailSender
{
    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        logger.LogInformation(
            "Email not sent (no provider configured). To: {To}. Subject: {Subject}",
            Mask(message.ToAddress), message.Subject);
        return Task.CompletedTask;
    }

    internal static string Mask(string address)
    {
        var at = address.IndexOf('@');
        return at <= 0 ? "***" : $"{address[0]}***{address[at..]}";
    }
}