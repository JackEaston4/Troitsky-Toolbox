namespace TroitskyToolbox.Storage;

/// <summary>
/// M16 Persistence module: the on-disk representation behind <see cref="IModelLibrary"/> — a
/// content file per model plus a JSON list of records (<c>index.json</c>). Declared deviation
/// from MG M16 / SRS P.5 (SQL Server) for the POC; swapping in a database later means replacing
/// the one class behind this interface.
/// </summary>
public interface IPersistence
{
    /// <summary>Reads the index; tolerates a missing file (fresh install) by returning an empty list.</summary>
    Task<IReadOnlyList<StoredModel>> LoadIndexAsync(CancellationToken ct = default);

    /// <summary>
    /// Overwrites the index with <paramref name="models"/> via a temp file + atomic rename.
    /// Callers must serialize their own calls to this (e.g. with one <see cref="System.Threading.SemaphoreSlim"/>)
    /// — this method does not lock on its own.
    /// </summary>
    Task SaveIndexAsync(IReadOnlyList<StoredModel> models, CancellationToken ct = default);

    /// <summary>A fresh path under the storage root to stream an upload into before it is validated.</summary>
    string CreateTempFilePath();

    /// <summary>Moves a validated temp file into place as that model's permanent <c>&lt;guid&gt;.step</c>.</summary>
    void CommitContent(string tempPath, Guid id);

    /// <summary>Best-effort delete of a temp file left over from a rejected or failed upload.</summary>
    void DeleteTempFile(string tempPath);

    /// <summary>Opens the committed content for <paramref name="id"/>, or null if it is not on disk.</summary>
    Task<Stream?> OpenContentAsync(Guid id, CancellationToken ct = default);
}
