using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
namespace Cafe.Api.Extensions;

public sealed class ProxyOptions
{
    public const string SectionName = "Proxy";

    /// <summary>Shared with the Next.js server. Lets it tell the API a visitor's real IP address.</summary>
    public string SharedSecret { get; set; } = string.Empty;
}

public static class ClientIp
{
    public const string SecretHeader = "X-Cafe-Proxy-Secret";
    public const string IpHeader = "X-Cafe-Client-Ip";

    /// <summary>The caller's IP for rate limiting. The IP header is trusted only with the right secret.</summary>
    public static string Resolve(HttpContext context)
    {
        var secret = context.RequestServices.GetRequiredService<IOptions<ProxyOptions>>().Value.SharedSecret;

        if (!string.IsNullOrEmpty(secret)
            && context.Request.Headers.TryGetValue(SecretHeader, out var provided)
            && Matches(provided.ToString(), secret)
            && context.Request.Headers.TryGetValue(IpHeader, out var claimed)
            && IPAddress.TryParse(claimed.ToString(), out var address))
        {
            return address.ToString();
        }

        return context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    }

    private static bool Matches(string provided, string expected) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(provided), Encoding.UTF8.GetBytes(expected));
}