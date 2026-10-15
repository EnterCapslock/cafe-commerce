using Cafe.Api.Data;
using Cafe.Api.Models.Enums;
using Cafe.Api.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
namespace Cafe.Api.Services.Notifications;

/// <summary>Sends queued emails, retries temporary failures with a growing delay, and tidies old rows.</summary>
public sealed class EmailOutboxWorker(IServiceScopeFactory scopes, TimeProvider time, ILogger<EmailOutboxWorker> logger)
    : BackgroundService
{
    // After failed attempt n, wait RetryDelays[n - 1]. After the last delay is used up, the email is marked Failed.
    internal static readonly TimeSpan[] RetryDelays =
    [
        TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(2), TimeSpan.FromHours(6)
    ];

    private const int BatchSize = 10;
    private static readonly TimeSpan Retention = TimeSpan.FromDays(30);
    private DateTime _lastCleanup = DateTime.MinValue;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5), time);

        do
        {
            try { await RunOnceAsync(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Email outbox run failed; will retry on the next tick");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    internal async Task RunOnceAsync(CancellationToken ct)
    {
        using var scope = scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sender = scope.ServiceProvider.GetRequiredService<IEmailSender>();
        var now = time.GetUtcNow().UtcDateTime;

        var due = await db.EmailOutbox
            .Where(e => e.Status == EmailStatus.Pending && e.NextAttemptAt <= now)
            .OrderBy(e => e.NextAttemptAt)
            .Take(BatchSize)
            .ToListAsync(ct);

        foreach (var item in due)
        {
            try
            {
                await sender.SendAsync(
                    new EmailMessage(item.ToAddress, item.ToName, item.Subject, item.HtmlBody, item.TextBody), ct);

                item.Status = EmailStatus.Sent;
                item.SentAt = now;
                item.LastError = null;
            }
            catch (EmailSendException ex)
            {
                item.Attempts++;
                item.LastError = ex.Message.Length <= 500 ? ex.Message : ex.Message[..500];

                if (!ex.Transient || item.Attempts > RetryDelays.Length)
                {
                    item.Status = EmailStatus.Failed;
                    logger.LogError("Email {EmailId} ({Kind}) failed for good after {Attempts} attempt(s): {Error}",
                        item.Id, item.Kind, item.Attempts, item.LastError);
                }
                else
                {
                    item.NextAttemptAt = now + RetryDelays[item.Attempts - 1];
                    logger.LogWarning("Email {EmailId} ({Kind}) failed (attempt {Attempts}); will retry at {Next}",
                        item.Id, item.Kind, item.Attempts, item.NextAttemptAt);
                }
            }

            await db.SaveChangesAsync(ct);   // after each email, so a crash can't resend the earlier ones
        }

        // Once an hour, delete finished rows older than the retention period.
        if (now - _lastCleanup >= TimeSpan.FromHours(1))
        {
            _lastCleanup = now;
            var cutoff = now - Retention;
            await db.EmailOutbox
                .Where(e => e.Status != EmailStatus.Pending && e.CreatedAt < cutoff)
                .ExecuteDeleteAsync(ct);
        }
    }
}