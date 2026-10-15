using System.Globalization;
using System.Net;
using System.Text;
using Cafe.Api.Models.Entities;
using Cafe.Api.Models.Enums;
using Cafe.Api.Services.Interfaces;
using Cafe.Api.Infrastructure.Mail;
using Cafe.Api.Services.Orders;
using Microsoft.Extensions.Options;
namespace Cafe.Api.Services.Notifications;

/// <summary>Turns an order into an email. Pure: no database, no network, easy to test.</summary>
public sealed class OrderEmailComposer(IOptions<EmailOptions> options, IConfiguration config)
{
    private readonly string _shop = string.IsNullOrWhiteSpace(options.Value.FromName) ? "The Cafe" : options.Value.FromName;
    private readonly string _site = (config["Frontend:BaseUrl"] ?? "http://localhost:3000").TrimEnd('/');

    public EmailMessage Compose(Order order, EmailKind kind)
    {
        var code = OrderCode.For(order.Id);
        var link = $"{_site}/orders/{order.Id}";
        var total = Peso(order.TotalAmount);
        var pickup = FormatPickup(order.PickupTime);
        var paidOnline = order.PaymentMethod == PaymentMethod.Online;

        string subject, heading, intro;
        if (kind == EmailKind.ReadyForPickup)
        {
            subject = $"Your order #{code} is ready for pickup";
            heading = "Your order is ready!";
            intro = "It's ready to collect whenever you arrive.";
        }
        else if (paidOnline)
        {
            subject = $"Payment received for order #{code}";
            heading = "Payment received";
            intro = "Thank you. Your payment went through, and we'll have your order ready for your pickup time.";
        }
        else
        {
            subject = $"We got your order #{code}";
            heading = "Order received";
            intro = "Thanks for ordering ahead. We'll have it ready for your pickup time.";
        }

        var payment = paidOnline ? $"Paid online: {total}" : $"Please pay {total} at the counter when you pick up.";
        var account = order.UserId is not null
            ? "This order is saved to your account. Sign in to see it."
            : $"Lost this email? Use Find my order ({_site}/find-order) with your order code and this email address.";

        var items = order.Items.OrderBy(i => i.Id).ToList();
        var lineText = items.Select(i => $"- {i.Quantity} x {i.ProductName} ({i.VariantName}): {Peso(i.PriceAtPurchase * i.Quantity)}");

        var text = new StringBuilder()
            .AppendLine(heading).AppendLine()
            .AppendLine($"Hi {order.CustomerName.Trim()}, {intro}").AppendLine()
            .AppendLine($"Order #{code}")
            .AppendLine($"Pickup: {pickup}").AppendLine()
            .AppendLine(string.Join(Environment.NewLine, lineText))
            .AppendLine($"Total: {total}")
            .AppendLine(payment).AppendLine()
            .AppendLine($"Track your order: {link}").AppendLine()
            .AppendLine(account).AppendLine()
            .AppendLine(_shop)
            .ToString();

        var rows = new StringBuilder();
        foreach (var i in items)
        {
            rows.Append("<tr>")
                .Append($"<td style=\"padding:4px 0;font-size:14px;\">{Enc($"{i.Quantity} × {i.ProductName} ({i.VariantName})")}</td>")
                .Append($"<td align=\"right\" style=\"padding:4px 0;font-size:14px;\">{Enc(Peso(i.PriceAtPurchase * i.Quantity))}</td>")
                .Append("</tr>");
        }

        var html = $$"""
            <!doctype html>
            <html>
            <body style="margin:0;background:#f5f1ea;font-family:Arial,Helvetica,sans-serif;color:#2b2420;">
              <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
                <tr><td align="center" style="padding:24px 12px;">
                  <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="max-width:520px;background:#ffffff;border-radius:12px;">
                    <tr><td style="padding:28px;">
                      <p style="margin:0 0 4px;font-size:13px;color:#7a6f66;">{{Enc(_shop)}}</p>
                      <h1 style="margin:0 0 14px;font-size:22px;">{{Enc(heading)}}</h1>
                      <p style="margin:0 0 16px;font-size:15px;line-height:1.5;">Hi {{Enc(order.CustomerName.Trim())}}, {{Enc(intro)}}</p>
                      <p style="margin:0 0 16px;font-size:15px;line-height:1.6;">
                        Order <strong>#{{Enc(code)}}</strong><br>
                        Pickup: <strong>{{Enc(pickup)}}</strong>
                      </p>
                      <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="margin:0 0 12px;">
                        {{rows}}
                        <tr>
                          <td style="padding-top:10px;border-top:1px solid #e6dfd6;font-size:15px;"><strong>Total</strong></td>
                          <td align="right" style="padding-top:10px;border-top:1px solid #e6dfd6;font-size:15px;"><strong>{{Enc(total)}}</strong></td>
                        </tr>
                      </table>
                      <p style="margin:0 0 20px;font-size:14px;color:#5b514a;">{{Enc(payment)}}</p>
                      <p style="margin:0 0 20px;">
                        <a href="{{Enc(link)}}" style="display:inline-block;background:#4a3426;color:#ffffff;padding:12px 20px;border-radius:8px;text-decoration:none;font-size:15px;">Track your order</a>
                      </p>
                      <p style="margin:0;font-size:12px;line-height:1.5;color:#7a6f66;">{{Enc(account)}}</p>
                    </td></tr>
                  </table>
                </td></tr>
              </table>
            </body>
            </html>
            """;

        return new EmailMessage(order.CustomerEmail, order.CustomerName.Trim(), subject, html, text);
    }

    private static string Enc(string value) => WebUtility.HtmlEncode(value);

    /// <summary>₱130 for whole amounts, ₱130.50 otherwise.</summary>
    private static string Peso(decimal amount) =>
        "₱" + (amount % 1 == 0
            ? amount.ToString("N0", CultureInfo.InvariantCulture)
            : amount.ToString("N2", CultureInfo.InvariantCulture));

    /// <summary>"Oct 10, 2026, 8:05 PM PHT". The Philippines has no daylight saving, so +08:00 is always right.</summary>
    private static string FormatPickup(DateTime pickupUtc)
    {
        var utc = DateTime.SpecifyKind(pickupUtc, DateTimeKind.Utc);
        var local = new DateTimeOffset(utc).ToOffset(TimeSpan.FromHours(8));
        return local.ToString("MMM d, yyyy, h:mm tt", CultureInfo.InvariantCulture) + " PHT";
    }
}