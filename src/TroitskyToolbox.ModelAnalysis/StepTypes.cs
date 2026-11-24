namespace TroitskyToolbox.ModelAnalysis;

public enum StepErrorKind
{
    UnsupportedExtension,
    TooLarge,
    NotStep,
    Malformed,
    NoGeometry,
    Timeout,
}

/// <summary>Why a file was rejected, with a message that can be shown to the member (FR-MA-1).</summary>
public sealed record StepError(StepErrorKind Kind, string Message);

/// <summary>What the server read from a STEP file, independent of the 3D mesh.</summary>
public sealed record StepInspection
{
    public required string FileName { get; init; }
    public required long SizeBytes { get; init; }
    public string? ProductName { get; init; }
    public string? OriginatingSystem { get; init; }
    public string? Schema { get; init; }

    /// <summary>Name of the length unit the geometry is defined in, e.g. "millimetre".</summary>
    public string? LengthUnit { get; init; }

    /// <summary>Material density stored in the file by the CAD tool, in g/cm³, if any.</summary>
    public double? DensityGPerCm3 { get; init; }

    public required int EntityCount { get; init; }
    public required int SolidCount { get; init; }
    public required int FaceCount { get; init; }

    /// <summary>Count of each surface type used by the faces (PLANE, CYLINDRICAL_SURFACE, ...).</summary>
    public required IReadOnlyDictionary<string, int> SurfaceTypes { get; init; }
}

/// <summary>Result of validating/parsing a STEP file (FR-MA-1, NFR-MA-1): the metadata on success,
/// or the rejection reason on failure. Shared between M7 (<see cref="IModelImport"/>) and
/// M18 (<see cref="IStepParser"/>).</summary>
public abstract record StepInspectionResult
{
    private protected StepInspectionResult() { }

    public sealed record Success(StepInspection Inspection) : StepInspectionResult;
    public sealed record Failure(StepError Error) : StepInspectionResult;
}
