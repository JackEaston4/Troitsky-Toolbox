using System.Security.Cryptography;
using TroitskyToolbox.ModelAnalysis;

namespace TroitskyToolbox.Storage;

/// <inheritdoc cref="IModelLibrary"/>
/// <remarks>
/// Loads the index once at startup (<see cref="InitializeAsync"/>), seeding it from a sample
/// folder when empty so the list is never blank (FR-MA-4 partial). The in-memory list and the
/// index file are only ever mutated together, under <see cref="_mutationLock"/>, so uploads
/// can't race each other (SR-MA-3, M8/M16).
/// </remarks>
public sealed class ModelLibrary(
    IPersistence persistence,
    IModelImport import,
    long maxUploadBytes,
    TimeSpan validationTimeout,
    string? sampleFolder = null) : IModelLibrary
{
    private readonly SemaphoreSlim _mutationLock = new(1, 1);
    private volatile IReadOnlyList<StoredModel> _models = [];
    private bool _initialized;
    /// <summary>
    /// Maximum upload in bytes
    /// </summary>
    private const int MaxUploadBytes = 81920;
    // Use binary MB
    /// <summary>
    /// Bytes (B) per Megabyte (MB)
    /// </summary>
    private const int BPerMb = 1_048_576; // 1024 * 1024

    /// <summary>
    /// Loads <c>index.json</c> and, if it was empty, seeds every top-level <c>.stp</c>/<c>.step</c>
    /// file (case-insensitive) in the sample folder (FR-MA-4 partial). Call once at startup.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized) return;
        await _mutationLock.WaitAsync(ct);
        try
        {
            if (_initialized) return;
            _models = await persistence.LoadIndexAsync(ct);
            _initialized = true;
        }
        finally
        {
            _mutationLock.Release();
        }

        if (_models.Count == 0 && sampleFolder is not null && Directory.Exists(sampleFolder))
        {
            IEnumerable<string> samples = Directory.EnumerateFiles(sampleFolder)
                .Where(f => Path.GetExtension(f).ToLowerInvariant() is ".stp" or ".step")
                .OrderBy(f => f, StringComparer.OrdinalIgnoreCase);
            foreach (string path in samples)
            {
                await using FileStream stream = File.OpenRead(path);
                await UploadModelAsync(stream, Path.GetFileName(path), ct);
            }
        }
    }

    /// <summary>
    /// List the models, ordered by file name
    /// </summary>
    /// <param name="ct">The cancellation token</param>
    /// <returns></returns>
    public Task<IReadOnlyList<StoredModel>> ListModelsAsync(CancellationToken ct = default)
    {
        return Task.FromResult<IReadOnlyList<StoredModel>>(
            _models.OrderBy(m => m.FileName, StringComparer.OrdinalIgnoreCase).ToList());
    }
        

    /// <summary>
    /// Get the model, or null
    /// </summary>
    /// <param name="id">The globally unique identifier</param>
    /// <param name="ct">The cancellation token</param>
    /// <returns></returns>
    public Task<StoredModel?> GetModelAsync(Guid id, CancellationToken ct = default)
    {
        return Task.FromResult(_models.FirstOrDefault(m => m.Id == id));
    }

    /// <summary>
    /// Open the committed content for <paramref name="id"/>, or null if not found
    /// </summary>
    /// <param name="id">The globally unique identifier</param>
    /// <param name="ct">The cancellation token</param>
    /// <returns>The opened file</returns>
    public Task<Stream?> OpenContentAsync(Guid id, CancellationToken ct = default)
    {
        return persistence.OpenContentAsync(id, ct);
    }

    /// <summary>
    /// Upload a model
    /// </summary>
    /// <param name="content">The model, in the form of an opened file</param>
    /// <param name="filePath">The file's path</param>
    /// <param name="ct">The cancellation token</param>
    /// <returns>The outcome of the attempted model upload</returns>
    public async Task<UploadResult> UploadModelAsync(Stream content, string filePath, CancellationToken ct = default)
    {
        string fileName = Path.GetFileName(filePath);
        string tempPath = persistence.CreateTempFilePath();
        long sizeBytes;
        string sha256;
        try
        {
            (sizeBytes, sha256) = await StreamToTempFileAsync(content, tempPath, ct);
        }
        catch (UploadTooLargeException)
        {
            persistence.DeleteTempFile(tempPath);
            return new UploadResult.Failure(new StepError(StepErrorKind.TooLarge,
                $"The file is larger than the {maxUploadBytes / (BPerMb)} MB limit."));
        }
        catch
        {
            persistence.DeleteTempFile(tempPath);
            throw;
        }

        // The parser needs the whole text; reading the (<= MaxUploadBytes) temp file back into
        // memory here is a second pass over the file, not the request body (no double buffering
        // of the upload stream itself). Validation always runs before the duplicate check, so an
        // upload with a wrong extension/garbage body is rejected even if its bytes happen to
        // match an already-stored file's hash.
        byte[] bytes;
        try
        {
            bytes = await File.ReadAllBytesAsync(tempPath, ct);
        }
        catch
        {
            persistence.DeleteTempFile(tempPath);
            throw;
        }

        StepInspectionResult validation = await import.ValidateAsync(bytes, filePath, maxUploadBytes, validationTimeout, ct);
        if (validation is StepInspectionResult.Failure failure)
        {
            persistence.DeleteTempFile(tempPath);
            return new UploadResult.Failure(failure.Error);
        }

        StoredModel? existing = _models.FirstOrDefault(m => m.Sha256 == sha256);
        if (existing is not null)
        {
            persistence.DeleteTempFile(tempPath);
            return new UploadResult.Success(existing, AlreadyStored: true);
        }

        var inspection = ((StepInspectionResult.Success)validation).Inspection;
        var id = Guid.NewGuid();
        var model = new StoredModel(id, filePath, sizeBytes, sha256, DateTimeOffset.UtcNow, inspection);

        await _mutationLock.WaitAsync(ct);
        try
        {
            // Re-check for a duplicate written by a concurrent upload while we were validating.
            StoredModel? raced = _models.FirstOrDefault(m => m.Sha256 == sha256);
            if (raced is not null)
            {
                persistence.DeleteTempFile(tempPath);
                return new UploadResult.Success(raced, AlreadyStored: true);
            }

            persistence.CommitContent(tempPath, id);
            List<StoredModel> updated = [.. _models, model];
            await persistence.SaveIndexAsync(updated, ct);
            _models = updated;
        }
        finally
        {
            _mutationLock.Release();
        }

        return new UploadResult.Success(model, AlreadyStored: false);
    }

    /// <summary>Streams the request body to a temp file while hashing it, enforcing
    /// <see cref="MaxUploadBytes" /> as it goes (so a hostile Content-Length header can't
    /// bypass the limit).</summary>
    private async Task<(long SizeBytes, string Sha256)> StreamToTempFileAsync(
        Stream content, string tempPath, CancellationToken ct)
    {
        using var hasher = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        long size = 0;
        var buffer = new byte[MaxUploadBytes];
        await using (var tempStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
        {
            int read;
            while ((read = await content.ReadAsync(buffer, ct)) > 0)
            {
                size += read;
                if (size > maxUploadBytes) throw new UploadTooLargeException();
                hasher.AppendData(buffer, 0, read);
                await tempStream.WriteAsync(buffer.AsMemory(0, read), ct);
            }
        }
        return (size, Convert.ToHexString(hasher.GetHashAndReset()).ToLowerInvariant());
    }

    private sealed class UploadTooLargeException : Exception;
}
