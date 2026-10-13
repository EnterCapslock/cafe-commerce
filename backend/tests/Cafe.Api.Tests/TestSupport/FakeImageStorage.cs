using System.Collections.Concurrent;
using Cafe.Api.Services.Interfaces;

namespace Cafe.Api.Tests.TestSupport;

public sealed class FakeImageStorage : IImageStorage
{
    private readonly ConcurrentQueue<string> _deleted = new();
    private int _counter;

    public bool IsConfigured { get; set; } = true;
    public IReadOnlyList<string> DeletedIds => _deleted.ToArray();

    public Task<StoredImage> UploadAsync(Stream content, string fileName, CancellationToken ct)
    {
        var id = $"cafe/products/fake-{Interlocked.Increment(ref _counter)}";
        return Task.FromResult(new StoredImage($"https://res.cloudinary.com/demo/image/upload/v1/{id}.jpg", id));
    }

    public Task DeleteAsync(string publicId)
    {
        _deleted.Enqueue(publicId);
        return Task.CompletedTask;
    }
}