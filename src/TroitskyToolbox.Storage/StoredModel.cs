using TroitskyToolbox.ModelAnalysis;

namespace TroitskyToolbox.Storage;

/// <summary>A model that has been accepted and stored (FR-KR-1, FR-KR-2, FR-MA-4 partial).</summary>
public sealed record StoredModel(
    Guid Id, string FileName, long SizeBytes, string Sha256, DateTimeOffset UploadedAt, StepInspection Inspection);


