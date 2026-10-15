using System.Collections.Concurrent;
using Cafe.Api.Services.Interfaces;

namespace Cafe.Api.Tests.TestSupport;

public sealed class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();
    private readonly ConcurrentQueue<bool> _failures = new();   // each entry: is the failure transient?

    public IReadOnlyList<EmailMessage> Sent => _sent.ToArray();

    /// <summary>Makes the next send(s) fail. Transient failures should be retried; permanent ones shouldn't.</summary>
    public void FailNext(bool transient, int times = 1)
    {
        for (var i = 0; i < times; i++) _failures.Enqueue(transient);
    }

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        if (_failures.TryDequeue(out var transient))
            throw new EmailSendException("Fake provider failure.", transient);

        _sent.Enqueue(message);
        return Task.CompletedTask;
    }
}