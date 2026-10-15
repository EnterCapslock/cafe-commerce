namespace Cafe.Api.Services.Interfaces;

public sealed record EmailMessage(string ToAddress, string ToName, string Subject, string HtmlBody, string TextBody);

public sealed class EmailSendException(string message, bool transient, Exception? inner = null)
    : Exception(message, inner)
{
    /// <summary>True when trying again later might work (a timeout, a rate limit, or a provider outage).</summary>
    public bool Transient { get; } = transient;
}

public interface IEmailSender
{
    /// <exception cref="EmailSendException">The email could not be sent.</exception>
    Task SendAsync(EmailMessage message, CancellationToken ct);
}