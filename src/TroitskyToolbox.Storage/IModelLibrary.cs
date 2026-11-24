namespace TroitskyToolbox.Storage;

/// <summary>
/// M8 Model Library module: the member-facing surface over the stored models (FR-KR-1, FR-KR-2).
/// Registered as a singleton in DI; one instance backs the whole app.
/// </summary>
public interface IModelLibrary
{
    /// <summary>All stored models, sorted by file name (case-insensitive, A–Z).</summary>
    Task<IReadOnlyList<StoredModel>> ListModelsAsync(CancellationToken ct = default);

    Task<StoredModel?> GetModelAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Streams <paramref name="content"/> to storage while hashing it, validates it through
    /// M7/M18 before committing, and records it. A duplicate SHA-256 returns the existing
    /// record with <c>AlreadyStored = true</c> instead of storing a second copy.
    /// </summary>
    Task<UploadResult> UploadModelAsync(Stream content, string fileName, CancellationToken ct = default);

    /// <summary>Opens the stored bytes for <paramref name="id"/>, or null if unknown. Caller disposes.</summary>
    Task<Stream?> OpenContentAsync(Guid id, CancellationToken ct = default);
}
