using System.Text.Json;

namespace TroitskyToolbox.Storage;

/// <inheritdoc cref="IPersistence"/>
/// <remarks>
/// Files live under <paramref name="storageRoot"/> as <c>&lt;guid&gt;.step</c>; the list lives in
/// <c>index.json</c> next to them. Index writes go through one <see cref="SemaphoreSlim"/> so two
/// concurrent uploads can't corrupt it (SR-MA-3, M16).
/// </remarks>
public sealed class FilePersistence(string storageRoot) : IPersistence
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly SemaphoreSlim _writeLock = new(1, 1);
    private readonly string _indexPath = Path.Combine(storageRoot, "index.json");

    public string StorageRoot { get; } = storageRoot;

    public async Task<IReadOnlyList<StoredModel>> LoadIndexAsync(CancellationToken ct = default)
    {
        Directory.CreateDirectory(StorageRoot);
        if (!File.Exists(_indexPath)) return [];

        await using FileStream stream = File.OpenRead(_indexPath);
        var models = await JsonSerializer.DeserializeAsync<List<StoredModel>>(stream, JsonOptions, ct);
        return models ?? [];
    }

    public async Task SaveIndexAsync(IReadOnlyList<StoredModel> models, CancellationToken ct = default)
    {
        Directory.CreateDirectory(StorageRoot);
        await _writeLock.WaitAsync(ct);
        try
        {
            string tempPath = Path.Combine(StorageRoot, $"{Guid.NewGuid():N}.index.tmp");
            await using (FileStream stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(stream, models, JsonOptions, ct);
            }
            File.Move(tempPath, _indexPath, overwrite: true);
        }
        finally
        {
            _writeLock.Release();
        }
    }

    public string CreateTempFilePath()
    {
        Directory.CreateDirectory(StorageRoot);
        return Path.Combine(StorageRoot, $"{Guid.NewGuid():N}.upload.tmp");
    }

    public void CommitContent(string tempPath, Guid id)
    {
        File.Move(tempPath, ContentPath(id), overwrite: false);
    }

    public void DeleteTempFile(string tempPath)
    {
        try { File.Delete(tempPath); }
        // File already deleted; no need to resurrect and redelete
        catch (IOException) { }
        // Temp files need not be deleted
        catch (UnauthorizedAccessException) { }
    }

    public Task<Stream?> OpenContentAsync(Guid id, CancellationToken ct = default)
    {
        string path = ContentPath(id);
        if (!File.Exists(path)) return Task.FromResult<Stream?>(null);
        return Task.FromResult<Stream?>(File.OpenRead(path));
    }

    private string ContentPath(Guid id) => Path.Combine(StorageRoot, $"{id:D}.step");
}
