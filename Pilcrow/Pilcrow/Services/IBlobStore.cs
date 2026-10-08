namespace Pilcrow.Services;

public interface IBlobStore
{
    Task<string> SaveAsync(string key, byte[] bytes, CancellationToken ct);
    Task<byte[]> ReadAsync(string key, CancellationToken ct);
    Task DeleteAsync(string key, CancellationToken ct);
}

public class LocalBlobStore(IConfiguration config) : IBlobStore
{
    private readonly string _root = config["BlobStore:Path"] ?? "blobs";

    public async Task<string> SaveAsync(string key, byte[] bytes, CancellationToken ct)
    {
        var path = Path.Combine(_root, key);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes, ct);
        return key;
    }

    public Task<byte[]> ReadAsync(string key, CancellationToken ct) =>
        File.ReadAllBytesAsync(Path.Combine(_root, key), ct);

    public Task DeleteAsync(string key, CancellationToken ct)
    {
        var path = Path.Combine(_root, key);
        if (File.Exists(path)) File.Delete(path);
        return Task.CompletedTask;
    }
}