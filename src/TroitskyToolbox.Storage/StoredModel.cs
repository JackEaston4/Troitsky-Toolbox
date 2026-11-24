using TroitskyToolbox.ModelAnalysis;

namespace TroitskyToolbox.Storage;

/// <summary>A model that has been accepted and stored (FR-KR-1, FR-KR-2, FR-MA-4 partial).</summary>
public sealed record StoredModel(
    Guid Id, string FileName, long SizeBytes, string Sha256, DateTimeOffset UploadedAt, StepInspection Inspection);

/// <summary>Outcome of <see cref="IModelLibrary.UploadModelAsync"/>.</summary>
public abstract record UploadResult
{
    private protected UploadResult() { }

    /// <summary><paramref name="AlreadyStored"/> is true when the SHA-256 matched an existing
    /// record and nothing new was written (FR-KR-2).</summary>
    public sealed record Success(StoredModel Model, bool AlreadyStored) : UploadResult;

    public sealed record Failure(StepError Error) : UploadResult;
}
