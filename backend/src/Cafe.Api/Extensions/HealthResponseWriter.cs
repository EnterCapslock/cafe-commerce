using Microsoft.Extensions.Diagnostics.HealthChecks;
namespace Cafe.Api.Extensions;

public static class HealthResponseWriter
{
    /// <summary>A small JSON report. It never includes exception details.</summary>
    public static Task WriteAsync(HttpContext context, HealthReport report)
    {
        var payload = new
        {
            status = report.Status.ToString(),
            totalDurationMs = (int)report.TotalDuration.TotalMilliseconds,
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                durationMs = (int)e.Value.Duration.TotalMilliseconds
            })
        };
        return context.Response.WriteAsJsonAsync(payload);
    }
}