using System.Text;
using Cafe.Api.Infrastructure.Images;
using Xunit;

namespace Cafe.Api.Tests.Unit.Images;

public class ImageSnifferTests
{
    private static byte[] Concat(params byte[][] parts) => parts.SelectMany(p => p).ToArray();
    private static byte[] Ascii(string text) => Encoding.ASCII.GetBytes(text);

    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01];
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D];
    private static readonly byte[] WebP = Concat(Ascii("RIFF"), [0x24, 0x00, 0x00, 0x00], Ascii("WEBP"));

    private static Task<string?> Detect(byte[] bytes) => ImageSniffer.DetectAsync(new MemoryStream(bytes), default);

    [Fact] public async Task A_jpeg_is_recognised() => Assert.Equal("jpg", await Detect(Jpeg));
    [Fact] public async Task A_png_is_recognised() => Assert.Equal("png", await Detect(Png));
    [Fact] public async Task A_webp_is_recognised() => Assert.Equal("webp", await Detect(WebP));

    [Fact]
    public async Task A_gif_is_not_accepted() =>
        Assert.Null(await Detect(Concat(Ascii("GIF89a"), new byte[6])));

    [Fact]
    public async Task A_text_file_renamed_to_jpg_is_not_accepted() =>
        Assert.Null(await Detect(Ascii("this is not an image at all")));

    [Fact]
    public async Task Other_riff_files_such_as_wav_are_not_accepted() =>
        Assert.Null(await Detect(Concat(Ascii("RIFF"), [0x24, 0x00, 0x00, 0x00], Ascii("WAVE"))));

    [Fact]
    public async Task An_empty_file_is_not_accepted() => Assert.Null(await Detect([]));

    [Fact]
    public async Task A_file_too_short_to_identify_is_not_accepted() => Assert.Null(await Detect([0xFF, 0xD8]));

    [Fact]
    public async Task The_stream_is_rewound_so_it_can_still_be_uploaded()
    {
        var stream = new MemoryStream(Jpeg);

        await ImageSniffer.DetectAsync(stream, default);

        Assert.Equal(0, stream.Position);
    }
}