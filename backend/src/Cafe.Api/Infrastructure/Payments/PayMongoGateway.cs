using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Cafe.Api.Models.Entities;
using Cafe.Api.Services.Interfaces;
using Microsoft.Extensions.Options;
namespace Cafe.Api.Infrastructure.Payments;

public class PayMongoGateway(
    HttpClient http,
    IOptions<PayMongoOptions> options,
    IConfiguration config,
    ILogger<PayMongoGateway> logger) : IPaymentGateway
{
    private readonly PayMongoOptions _opts = options.Value;

    public async Task<CheckoutSession> CreateCheckoutAsync(Order order, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opts.SecretKey))
            throw new PaymentGatewayException("PayMongo:SecretKey is not configured.");

        var frontend = (config["Frontend:BaseUrl"] ?? "http://localhost:3000").TrimEnd('/');
        var returnUrl = $"{frontend}/orders/{order.Id}";   // status page; shows Paid or "Complete payment"
        var methods = _opts.PaymentMethodTypes.Length > 0
            ? _opts.PaymentMethodTypes
            : ["gcash", "paymaya", "grab_pay", "qrph", "card"];

        var payload = new
        {
            data = new
            {
                attributes = new
                {
                    line_items = order.Items.Select(i => new
                    {
                        name = $"{i.ProductName} ({i.VariantName})",
                        amount = ToCentavos(i.PriceAtPurchase),
                        currency = "PHP",
                        quantity = i.Quantity
                    }).ToArray(),
                    payment_method_types = methods,
                    success_url = returnUrl,
                    cancel_url = returnUrl,
                    reference_number = order.Id.ToString()
                }
            }
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "v2/checkout_sessions")
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(_opts.SecretKey + ":")));

        try
        {
            using var response = await http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("PayMongo rejected checkout for order {OrderId}: {Status} {Body}",
                    order.Id, (int)response.StatusCode, body);
                throw new PaymentGatewayException($"PayMongo returned {(int)response.StatusCode}.");
            }

            using var doc = JsonDocument.Parse(body);
            var data = doc.RootElement.GetProperty("data");
            return new CheckoutSession(
                data.GetProperty("id").GetString()!,
                data.GetProperty("attributes").GetProperty("checkout_url").GetString()!);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or KeyNotFoundException
                                   || (ex is TaskCanceledException && !ct.IsCancellationRequested))
        {
            throw new PaymentGatewayException("Could not reach PayMongo.", ex);
        }
    }

    public bool VerifyWebhookSignature(string signatureHeader, string rawBody)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader) || string.IsNullOrWhiteSpace(_opts.WebhookSecret))
            return false;

        string? t = null, te = null, li = null;
        foreach (var part in signatureHeader.Split(','))
        {
            var kv = part.Split('=', 2);
            if (kv.Length != 2) continue;
            switch (kv[0].Trim())
            {
                case "t": t = kv[1].Trim(); break;
                case "te": te = kv[1].Trim(); break;
                case "li": li = kv[1].Trim(); break;
            }
        }

        // Test keys receive the "te" signature, live keys the "li" signature.
        var provided = _opts.SecretKey.StartsWith("sk_live_", StringComparison.Ordinal) ? li : te;
        if (string.IsNullOrEmpty(t) || string.IsNullOrEmpty(provided)) return false;

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_opts.WebhookSecret));
        var computed = Convert.ToHexStringLower(hmac.ComputeHash(Encoding.UTF8.GetBytes($"{t}.{rawBody}")));

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(computed),
            Encoding.ASCII.GetBytes(provided.ToLowerInvariant()));
    }

    private static long ToCentavos(decimal pesos) =>
        (long)Math.Round(pesos * 100m, MidpointRounding.AwayFromZero);
}