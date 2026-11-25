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

    /// <summary>
    /// Imports any top-level <c>.stp</c>/<c>.step</c> file from the sample folder that isn't
    /// already stored (FR-KR-1, FR-KR-2). Each file goes through the same validate-then-dedupe
    /// path as <see cref="UploadModelAsync"/>; a validation failure is reported in the result,
    /// not thrown, and does not stop the rest of the sync. Returns all zeros if the sample
    /// folder doesn't exist. Runs once automatically on startup and again on demand (the
    /// explorer's refresh button).
    /// </summary>
    Task<RefreshResult> RefreshAsync(CancellationToken ct = default);
}
