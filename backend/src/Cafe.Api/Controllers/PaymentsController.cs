using System.Text;
using System.Text.Json;
using Cafe.Api.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Cafe.Api.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController(
    IPaymentGateway gateway,
    IOrderService orders,
    ILogger<PaymentsController> logger) : ControllerBase
{
    private const string PaidEvent = "checkout_session.payment.paid";

    /// <summary>PayMongo webhook. Verified by signature, never by login.</summary>
    [HttpPost("webhook")]
    [AllowAnonymous]
    public async Task<IActionResult> Webhook(CancellationToken ct)
    {
        // The signature covers the exact raw bytes, so read the body before any parsing.
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var rawBody = await reader.ReadToEndAsync(ct);

        if (!gateway.VerifyWebhookSignature(Request.Headers["Paymongo-Signature"].ToString(), rawBody))
        {
            logger.LogWarning("Rejected webhook with an invalid signature");
            return Unauthorized();
        }

        if (!TryParsePaidEvent(rawBody, out var sessionId, out var reference))
        {
            // Not a paid event, or an unexpected shape. Acknowledge so PayMongo doesn't retry.
            logger.LogInformation("Webhook acknowledged but not processed. Body: {Body}", rawBody);
            return Ok(new { received = true });
        }

        if (!Guid.TryParse(reference, out var orderId))
        {
            logger.LogWarning("Paid event for session {SessionId} has a non-order reference", sessionId);
            return Ok(new { received = true });
        }

        // A database failure here throws -> 500 -> PayMongo retries, which is what we want.
        await orders.MarkPaidAsync(orderId, sessionId!, ct);
        return Ok(new { received = true });
    }

    private static bool TryParsePaidEvent(string rawBody, out string? sessionId, out string? reference)
    {
        sessionId = reference = null;
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            if (!doc.RootElement.TryGetProperty("data", out var data)
                || !data.TryGetProperty("attributes", out var attrs)
                || !attrs.TryGetProperty("type", out var type)
                || type.GetString() != PaidEvent
                || !attrs.TryGetProperty("data", out var session)
                || !session.TryGetProperty("id", out var id)
                || !session.TryGetProperty("attributes", out var sessionAttrs)
                || !sessionAttrs.TryGetProperty("reference_number", out var refNo))
                return false;

            sessionId = id.GetString();
            reference = refNo.GetString();
            return sessionId is not null && reference is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}