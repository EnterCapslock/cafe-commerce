using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Cafe.Api.Tests.TestSupport;

/// <summary>Builds and signs PayMongo webhook requests the way PayMongo does.</summary>
public static class Webhooks
{
    public static string PaidEvent(string sessionId, string reference, string eventType = "checkout_session.payment.paid") =>
        JsonSerializer.Serialize(new
        {
            data = new
            {
                id = $"evt_{Guid.NewGuid():N}",
                attributes = new
                {
                    type = eventType,
                    livemode = false,
                    data = new { id = sessionId, attributes = new { reference_number = reference } }
                }
            }
        });

    /// <summary>The Paymongo-Signature header value for a body (test-mode "te" signature).</summary>
    public static string Sign(string body, string? secret = null, long? timestamp = null)
    {
        var t = (timestamp ?? DateTimeOffset.UtcNow.ToUnixTimeSeconds()).ToString();
        var mac = Convert.ToHexStringLower(HMACSHA256.HashData(
            Encoding.UTF8.GetBytes(secret ?? TestEnvironment.PayMongoWebhookSecret),
            Encoding.UTF8.GetBytes($"{t}.{body}")));
        return $"t={t},te={mac},li=";
    }
}