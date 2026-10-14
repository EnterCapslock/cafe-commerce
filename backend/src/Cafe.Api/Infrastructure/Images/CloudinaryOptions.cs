namespace Cafe.Api.Infrastructure.Images;

public sealed class CloudinaryOptions
{
    public const string SectionName = "Cloudinary";

    public string CloudName { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;   // user-secrets only
    public string Folder { get; set; } = "cafe/products";
}