namespace Cafe.Api.Services;

public sealed class PayMongoOptions
{
    public const string SectionName = "PayMongo";

    public string BaseUrl { get; set; } = "https://api.paymongo.com/";
    public string SecretKey { get; set; } = string.Empty;       // user-secrets
    public string WebhookSecret { get; set; } = string.Empty;   // user-secrets
    public string[] PaymentMethodTypes { get; set; } = [];      // set in appsettings.json
}