// FakePayMongoHandler.cs
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Cafe.Api.Tests.TestSupport;

/// <summary>
/// Stands in for PayMongo's network. The real PayMongoGateway runs against it, so request
/// building and error handling are tested for real.
/// </summary>
public sealed class FakePayMongoHandler : HttpMessageHandler
{
    private readonly ConcurrentDictionary<string, string> _sessions = new();
    private int _requestCount;

    /// <summary>When true, PayMongo "is down" and answers 503.</summary>
    public bool Fail { get; set; }

    public string? LastBody { get; private set; }
    public AuthenticationHeaderValue? LastAuthorization { get; private set; }
    public int RequestCount => _requestCount;

    /// <summary>The checkout session this fake created for an order.</summary>
    public string SessionFor(Guid orderId) => _sessions[orderId.ToString()];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _requestCount);
        LastAuthorization = request.Headers.Authorization;
        LastBody = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);

        if (Fail) return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);

        using var document = JsonDocument.Parse(LastBody);
        var reference = document.RootElement
            .GetProperty("data").GetProperty("attributes").GetProperty("reference_number").GetString()!;

        var sessionId = $"cs_{Guid.NewGuid():N}";
        _sessions[reference] = sessionId;

        var json = $$"""{"data":{"id":"{{sessionId}}","attributes":{"checkout_url":"https://checkout.paymongo.test/{{sessionId}}"} } } """;
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }
}