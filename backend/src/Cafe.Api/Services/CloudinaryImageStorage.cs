using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using Cafe.Api.Services.Interfaces;
using Microsoft.Extensions.Options;
namespace Cafe.Api.Services;

public sealed class CloudinaryImageStorage : IImageStorage
{
    private readonly Cloudinary? _cloudinary;
    private readonly CloudinaryOptions _opts;
    private readonly ILogger<CloudinaryImageStorage> _logger;

    public CloudinaryImageStorage(IOptions<CloudinaryOptions> options, ILogger<CloudinaryImageStorage> logger)
    {
        _opts = options.Value;
        _logger = logger;

        if (IsConfigured)
        {
            _cloudinary = new Cloudinary(new Account(_opts.CloudName, _opts.ApiKey, _opts.ApiSecret));
            _cloudinary.Api.Secure = true;
        }
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_opts.CloudName)
        && !string.IsNullOrWhiteSpace(_opts.ApiKey)
        && !string.IsNullOrWhiteSpace(_opts.ApiSecret);

    public async Task<StoredImage> UploadAsync(Stream content, string fileName, CancellationToken ct)
    {
        if (_cloudinary is null) throw new ImageStorageException("Cloudinary isn't configured.");
        ct.ThrowIfCancellationRequested();

        ImageUploadResult result;
        try
        {
            result = await _cloudinary.UploadAsync(new ImageUploadParams
            {
                File = new FileDescription(fileName, content),
                Folder = _opts.Folder,
                UseFilename = false,
                UniqueFilename = true,
                Overwrite = false,
                // Stored images never exceed 1600px on a side; delivery sizes come from the URL.
                Transformation = new Transformation().Width(1600).Height(1600).Crop("limit")
            });
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            throw new ImageStorageException("Could not reach Cloudinary.", ex);
        }

        if (result.Error is not null || result.SecureUrl is null)
            throw new ImageStorageException(result.Error?.Message ?? "Cloudinary returned no URL.");

        return new StoredImage(result.SecureUrl.ToString(), result.PublicId);
    }

    public async Task DeleteAsync(string publicId)
    {
        if (_cloudinary is null || string.IsNullOrWhiteSpace(publicId)) return;
        try
        {
            var result = await _cloudinary.DestroyAsync(new DeletionParams(publicId));
            if (result.Error is not null)
                _logger.LogWarning("Could not delete image {PublicId}: {Error}", publicId, result.Error.Message);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete image {PublicId}", publicId);
        }
    }
}