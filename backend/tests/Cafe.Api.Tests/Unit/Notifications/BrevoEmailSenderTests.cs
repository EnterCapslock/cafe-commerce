using System.Net;
using System.Text;
using System.Text.Json;
using Cafe.Api.Services.Interfaces;
using Cafe.Api.Infrastructure.Mail;
using Cafe.Api.Tests.TestSupport;
using Microsoft.Extensions.Options;
using Xunit;

namespace Cafe.Api.Tests.Unit.Notifications;

public class BrevoEmailSenderTests
{
    private static readonly EmailMessage Message =
        new("juan@example.com", "Juan", "We got your order", "<p>Hello</p>", "Hello");

    private static HttpResponseMessage Reply(HttpStatusCode status, string body = "{}") =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static BrevoEmailSender Sender(StubHttpHandler handler, string apiKey = "key-123", string from = "shop@example.com") =>
        new(new HttpClient(handler) { BaseAddress = new Uri("https://api.brevo.com/") },
            Options.Create(new EmailOptions { BrevoApiKey = apiKey, FromAddress = from, FromName = "The Cafe" }));

    [Fact]
    public async Task It_sends_an_authenticated_request_with_the_message_and_the_verified_sender()
    {
        var handler = new StubHttpHandler((_, _) => Reply(HttpStatusCode.Created, """{"messageId":"<1@x>"}"""));

        await Sender(handler).SendAsync(Message, default);

        Assert.Equal(HttpMethod.Post, handler.Request!.Method);
        Assert.Equal("https://api.brevo.com/v3/smtp/email", handler.Request.RequestUri!.ToString());
        Assert.Equal("key-123", handler.Request.Headers.GetValues("api-key").Single());

        using var body = JsonDocument.Parse(handler.Body!);
        var root = body.RootElement;
        Assert.Equal("shop@example.com", root.GetProperty("sender").GetProperty("email").GetString());
        Assert.Equal("The Cafe", root.GetProperty("sender").GetProperty("name").GetString());
        Assert.Equal("juan@example.com", root.GetProperty("to")[0].GetProperty("email").GetString());
        Assert.Equal("We got your order", root.GetProperty("subject").GetString());
        Assert.Equal("<p>Hello</p>", root.GetProperty("htmlContent").GetString());
        Assert.Equal("Hello", root.GetProperty("textContent").GetString());
    }

    [Theory]
    [InlineData(HttpStatusCode.BadRequest, false)]            // for example, a sender that isn't verified
    [InlineData(HttpStatusCode.Unauthorized, false)]          // a wrong API key
    [InlineData(HttpStatusCode.Forbidden, false)]
    [InlineData(HttpStatusCode.RequestTimeout, true)]
    [InlineData(HttpStatusCode.TooManyRequests, true)]        // the daily limit
    [InlineData(HttpStatusCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    public async Task A_rejection_is_marked_retryable_only_when_waiting_could_help(HttpStatusCode status, bool transient)
    {
        var handler = new StubHttpHandler((_, _) => Reply(status, """{"message":"nope"}"""));

        var error = await Assert.ThrowsAsync<EmailSendException>(() => Sender(handler).SendAsync(Message, default));

        Assert.Equal(transient, error.Transient);
        Assert.Contains(((int)status).ToString(), error.Message);
    }

    [Fact]
    public async Task A_network_failure_is_retryable()
    {
        var handler = new StubHttpHandler((_, _) => throw new HttpRequestException("connection refused"));

        var error = await Assert.ThrowsAsync<EmailSendException>(() => Sender(handler).SendAsync(Message, default));

        Assert.True(error.Transient);
    }

    [Theory]
    [InlineData("", "shop@example.com")]
    [InlineData("key-123", "")]
    public async Task Without_a_key_or_sender_it_fails_without_calling_the_network(string apiKey, string from)
    {
        var handler = new StubHttpHandler((_, _) => Reply(HttpStatusCode.Created));

        var error = await Assert.ThrowsAsync<EmailSendException>(() => Sender(handler, apiKey, from).SendAsync(Message, default));

        Assert.False(error.Transient);
        Assert.Null(handler.Request);
    }

    [Theory]
    [InlineData("juan@example.com", "j***@example.com")]
    [InlineData("a@b.co", "a***@b.co")]
    [InlineData("nonsense", "***")]
    public void The_log_sender_never_prints_a_full_address(string address, string masked)
    {
        Assert.Equal(masked, LogEmailSender.Mask(address));
    }
}