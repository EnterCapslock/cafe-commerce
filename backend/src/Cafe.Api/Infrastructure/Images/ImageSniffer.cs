namespace Cafe.Api.Infrastructure.Images;

/// <summary>Identifies an image by its first bytes, so a renamed text file can't pass as a photo.</summary>
internal static class ImageSniffer
{
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <returns>"jpg", "png", "webp", or null if it isn't one of those. The stream is rewound.</returns>
    public static async Task<string?> DetectAsync(Stream stream, CancellationToken ct)
    {
        var header = new byte[12];
        var read = await stream.ReadAtLeastAsync(header, header.Length, throwOnEndOfStream: false, ct);
        stream.Position = 0;

        if (read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF) return "jpg";
        if (read >= 8 && header.AsSpan(0, 8).SequenceEqual(Png)) return "png";
        if (read >= 12 && header.AsSpan(0, 4).SequenceEqual("RIFF"u8) && header.AsSpan(8, 4).SequenceEqual("WEBP"u8))
            return "webp";
        return null;
    }
}