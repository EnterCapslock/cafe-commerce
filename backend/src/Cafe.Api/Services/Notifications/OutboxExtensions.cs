using Cafe.Api.Data;
using Cafe.Api.Models.Entities;
using Cafe.Api.Models.Enums;
using Cafe.Api.Services.Interfaces;
namespace Cafe.Api.Services.Notifications;

internal static class OutboxExtensions
{
    /// <summary>
    /// Adds an email to the outbox. It is saved by the caller's next SaveChanges, so it commits
    /// (or doesn't) together with the change that caused it.
    /// </summary>
    public static void EnqueueEmail(
        this AppDbContext db, EmailMessage message, EmailKind kind, Guid orderId, DateTime nowUtc) =>
        db.EmailOutbox.Add(new EmailOutboxItem
        {
            OrderId = orderId,
            Kind = kind,
            ToAddress = message.ToAddress,
            ToName = message.ToName,
            Subject = message.Subject,
            HtmlBody = message.HtmlBody,
            TextBody = message.TextBody,
            Status = EmailStatus.Pending,
            NextAttemptAt = nowUtc,
            CreatedAt = nowUtc
        });
}