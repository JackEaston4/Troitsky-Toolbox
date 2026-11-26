using TroitskyToolbox.ModelAnalysis;

namespace TroitskyToolbox.Storage;

/// <summary>Outcome of <see cref="IModelLibrary.UploadModelAsync"/>.</summary>
public abstract record UploadResult
{
    private protected UploadResult() { }

    /// <summary><paramref name="AlreadyStored"/> is true when the SHA-256 matched an existing
    /// record and nothing new was written (FR-KR-2).</summary>
    public sealed record Success(StoredModel Model, bool AlreadyStored) : UploadResult;
    /// <summary>
    /// Upload failure
    /// </summary>
    /// <param name="Error">the error that caused the upload to fail</param>
    public sealed record Failure(StepError Error) : UploadResult;
}
