using System.Net.Http.Headers;
using System.Net.Http.Json;
using Cafe.Api.Services.Interfaces;
using Microsoft.Extensions.Options;
namespace Cafe.Api.Infrastructure.Mail;

/// <summary>Sends through Brevo's HTTP API. (SMTP is often blocked on free hosting; HTTPS never is.)</summary>
public sealed class BrevoEmailSender(HttpClient http, IOptions<EmailOptions> options) : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var o = options.Value;
        if (!o.IsConfigured)
            throw new EmailSendException("Email isn't configured.", transient: false);

        var payload = new
        {
            sender = new { name = o.FromName, email = o.FromAddress },
            to = new[] { new { email = message.ToAddress, name = message.ToName } },
            subject = message.Subject,
            htmlContent = message.HtmlBody,
            textContent = message.TextBody
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "v3/smtp/email")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("api-key", o.BrevoApiKey);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        try
        {
            using var response = await http.SendAsync(request, ct);
            if (response.IsSuccessStatusCode) return;

            var body = await response.Content.ReadAsStringAsync(ct);
            var status = (int)response.StatusCode;

            // Timeouts, rate limits, and provider outages can recover. A rejected sender, key, or address won't.
            var transient = status is 408 or 429 or >= 500;
            throw new EmailSendException($"Brevo returned {status}: {Truncate(body, 300)}", transient);
        }
        catch (Exception ex) when (ex is HttpRequestException
                                   || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            throw new EmailSendException("Could not reach Brevo.", transient: true, ex);
        }
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];
}