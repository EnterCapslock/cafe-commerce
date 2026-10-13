using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Xunit.Sdk;

namespace Cafe.Api.Tests.TestSupport;

public static class TestJson
{
    /// <summary>The same JSON rules the API uses: camelCase, and enums as strings.</summary>
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}

public static class HttpExtensions
{
    public static Task<HttpResponseMessage> Call(
        this HttpClient client,
        HttpMethod method,
        string url,
        object? body = null,
        string? token = null,
        Action<HttpRequestMessage>? configure = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body, body.GetType(), null, TestJson.Options);
        configure?.Invoke(request);
        return client.SendAsync(request);
    }

    /// <summary>Fails with the response body in the message, which makes failures easy to read.</summary>
    public static async Task<HttpResponseMessage> ExpectAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        if (response.StatusCode != expected)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new XunitException(
                $"Expected {(int)expected} {expected}, but the API returned {(int)response.StatusCode} {response.StatusCode}.{Environment.NewLine}{body}");
        }
        return response;
    }

    public static async Task<T> ReadAsync<T>(this HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        await response.ExpectAsync(expected);
        return await response.Content.ReadFromJsonAsync<T>(TestJson.Options)
               ?? throw new XunitException("The response body was empty.");
    }

    public static async Task<ProblemDetails> ReadProblemAsync(this HttpResponseMessage response, HttpStatusCode expected)
    {
        await response.ExpectAsync(expected);
        return await response.Content.ReadFromJsonAsync<ProblemDetails>(TestJson.Options)
               ?? throw new XunitException("The response body was empty.");
    }

    public static async Task<ValidationProblemDetails> ReadValidationAsync(
        this HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.BadRequest)
    {
        await response.ExpectAsync(expected);
        return await response.Content.ReadFromJsonAsync<ValidationProblemDetails>(TestJson.Options)
               ?? throw new XunitException("The response body was empty.");
    }
}