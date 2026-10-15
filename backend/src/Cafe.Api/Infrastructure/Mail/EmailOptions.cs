namespace Cafe.Api.Infrastructure.Mail;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public string BrevoApiKey { get; set; } = string.Empty;   // user-secrets or the host's environment only
    public string FromAddress { get; set; } = string.Empty;   // must be a sender verified in Brevo
    public string FromName { get; set; } = "The Cafe";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(BrevoApiKey) && !string.IsNullOrWhiteSpace(FromAddress);
}