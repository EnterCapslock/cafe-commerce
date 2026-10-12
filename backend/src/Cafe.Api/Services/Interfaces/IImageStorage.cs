namespace Cafe.Api.Services.Interfaces;

public sealed record StoredImage(string Url, string PublicId);

public sealed class ImageStorageException(string message, Exception? inner = null)
    : Exception(message, inner);

public interface IImageStorage
{
    bool IsConfigured { get; }

    /// <exception cref="ImageStorageException">The upload failed.</exception>
    Task<StoredImage> UploadAsync(Stream content, string fileName, CancellationToken ct);

    /// <summary>Best effort: failures are logged, never thrown.</summary>
    Task DeleteAsync(string publicId);
}