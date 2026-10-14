using System.Security.Cryptography;
using System.Text;
using Cafe.Api.Infrastructure.Payments;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Cafe.Api.Tests.Unit.Payments;

public class PayMongoSignatureTests
{
    private const string TestKey = "sk_test_unit";
    private const string LiveKey = "sk_live_unit";
    private const string WebhookSecret = "whsk_unit_test";
    private const string Timestamp = "1700000000";
    private const string Body = """{"data":{"id":"evt_1","attributes":{"type":"checkout_session.payment.paid"}}}""";

    private static PayMongoGateway Gateway(string secretKey = TestKey, string webhookSecret = WebhookSecret) => new(
        new HttpClient(),
        Options.Create(new PayMongoOptions { SecretKey = secretKey, WebhookSecret = webhookSecret }),
        new ConfigurationBuilder().Build(),
        NullLogger<PayMongoGateway>.Instance);

    private static string Sign(string secret, string timestamp, string body) =>
        Convert.ToHexStringLower(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes($"{timestamp}.{body}")));

    private static string Header(string? t, string? te, string? li) => $"t={t},te={te},li={li}";

    [Fact]
    public void A_correct_test_mode_signature_is_accepted()
    {
        var header = Header(Timestamp, Sign(WebhookSecret, Timestamp, Body), "");
        Assert.True(Gateway().VerifyWebhookSignature(header, Body));
    }

    [Fact]
    public void The_header_parts_can_come_in_any_order_with_extra_spaces()
    {
        var header = $"li=, te={Sign(WebhookSecret, Timestamp, Body)} , t={Timestamp}";
        Assert.True(Gateway().VerifyWebhookSignature(header, Body));
    }

    [Fact]
    public void An_upper_case_signature_is_accepted()
    {
        var header = Header(Timestamp, Sign(WebhookSecret, Timestamp, Body).ToUpperInvariant(), "");
        Assert.True(Gateway().VerifyWebhookSignature(header, Body));
    }

    [Fact]
    public void A_tampered_body_is_rejected()
    {
        var header = Header(Timestamp, Sign(WebhookSecret, Timestamp, Body), "");
        Assert.False(Gateway().VerifyWebhookSignature(header, Body.Replace("evt_1", "evt_2")));
    }

    [Fact]
    public void A_signature_made_with_another_secret_is_rejected()
    {
        var header = Header(Timestamp, Sign("someone-elses-secret", Timestamp, Body), "");
        Assert.False(Gateway().VerifyWebhookSignature(header, Body));
    }

    [Fact]
    public void The_signature_is_tied_to_its_timestamp()
    {
        var header = Header("1700000001", Sign(WebhookSecret, Timestamp, Body), "");
        Assert.False(Gateway().VerifyWebhookSignature(header, Body));
    }

    [Fact]
    public void A_truncated_signature_is_rejected()
    {
        var header = Header(Timestamp, Sign(WebhookSecret, Timestamp, Body)[..20], "");
        Assert.False(Gateway().VerifyWebhookSignature(header, Body));
    }

    [Fact]
    public void A_live_key_accepts_the_live_signature()
    {
        var header = Header(Timestamp, "", Sign(WebhookSecret, Timestamp, Body));
        Assert.True(Gateway(secretKey: LiveKey).VerifyWebhookSignature(header, Body));
    }

    [Fact]
    public void A_live_key_does_not_accept_the_test_signature()
    {
        var header = Header(Timestamp, Sign(WebhookSecret, Timestamp, Body), "");
        Assert.False(Gateway(secretKey: LiveKey).VerifyWebhookSignature(header, Body));
    }

    [Fact]
    public void A_test_key_does_not_accept_the_live_signature()
    {
        var header = Header(Timestamp, "", Sign(WebhookSecret, Timestamp, Body));
        Assert.False(Gateway().VerifyWebhookSignature(header, Body));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("garbage")]
    [InlineData("te=abc,li=")]        // no timestamp
    [InlineData("t=1700000000")]      // no signature
    public void A_missing_or_malformed_header_is_rejected(string header)
    {
        Assert.False(Gateway().VerifyWebhookSignature(header, Body));
    }

    [Fact]
    public void Nothing_is_accepted_when_no_webhook_secret_is_configured()
    {
        var header = Header(Timestamp, Sign("", Timestamp, Body), "");
        Assert.False(Gateway(webhookSecret: "").VerifyWebhookSignature(header, Body));
    }
}