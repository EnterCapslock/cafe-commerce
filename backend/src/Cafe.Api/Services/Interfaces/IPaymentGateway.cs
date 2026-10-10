using Cafe.Api.Models.Entities;
namespace Cafe.Api.Services.Interfaces;

public sealed record CheckoutSession(string Id, string Url);

public sealed class PaymentGatewayException(string message, Exception? inner = null)
    : Exception(message, inner);

public interface IPaymentGateway
{
    /// <exception cref="PaymentGatewayException">The gateway is unreachable, misconfigured, or rejected the request.</exception>
    Task<CheckoutSession> CreateCheckoutAsync(Order order, CancellationToken ct);

    bool VerifyWebhookSignature(string signatureHeader, string rawBody);
}