using Cafe.Api.Services.Orders;
using Xunit;

namespace Cafe.Api.Tests.Unit.Orders;

public class LookupThrottleTests
{
    [Fact]
    public void An_email_gets_ten_attempts_an_hour()
    {
        using var throttle = new LookupThrottle();

        for (var i = 0; i < LookupThrottle.PermitsPerHour; i++)
            Assert.True(throttle.TryAcquire("target@example.com"));

        Assert.False(throttle.TryAcquire("target@example.com"));
    }

    [Fact]
    public void One_emails_attempts_do_not_use_up_another_emails()
    {
        using var throttle = new LookupThrottle();
        for (var i = 0; i < LookupThrottle.PermitsPerHour; i++) throttle.TryAcquire("busy@example.com");

        Assert.False(throttle.TryAcquire("busy@example.com"));
        Assert.True(throttle.TryAcquire("calm@example.com"));
    }
}