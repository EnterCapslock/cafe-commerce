namespace Cafe.Api.Services.Orders;

public static class OrderCode
{
    /// <summary>The code shown on an order page: the first 8 characters of its id, in capitals.</summary>
    public static string For(Guid id) => id.ToString("N")[..8].ToUpperInvariant();

    /// <summary>
    /// Turns whatever the customer typed ("#e18b 681f", "E18B-681F", ...) into 8 lower-case hex characters,
    /// or null if it can't be a code.
    /// </summary>
    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var cleaned = new string(input.Where(c => !char.IsWhiteSpace(c) && c != '#' && c != '-').ToArray());
        return cleaned.Length == 8 && cleaned.All(Uri.IsHexDigit) ? cleaned.ToLowerInvariant() : null;
    }
}