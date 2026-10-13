namespace Cafe.Api.Tests.TestSupport;

public static class TestData
{
    public const string StrongPassword = "Passw0rd!x";

    public static string Unique(string prefix) => $"{prefix} {Guid.NewGuid().ToString("N")[..8]}";

    /// <summary>A valid Philippine mobile number that no other test uses.</summary>
    public static string UniquePhone() => "09" + Random.Shared.Next(100_000_000, 999_999_999);
}