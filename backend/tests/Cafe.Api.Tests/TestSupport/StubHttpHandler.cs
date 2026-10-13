namespace Cafe.Api.Tests.TestSupport;

/// <summary>A fake network: records the request it receives and returns whatever the test says.</summary>
public sealed class StubHttpHandler(Func<HttpRequestMessage, string, HttpResponseMessage> respond) : HttpMessageHandler
{
    public HttpRequestMessage? Request { get; private set; }
    public string? Body { get; private set; }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Request = request;
        Body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(ct);
        return respond(request, Body);
    }
}