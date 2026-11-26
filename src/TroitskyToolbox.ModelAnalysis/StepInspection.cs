namespace TroitskyToolbox.ModelAnalysis;

/// <summary>What the server read from a STEP file, independent of the 3D mesh.</summary>
public sealed record StepInspection
{
    /// <summary>
    /// The file's immediate name (not its full path)
    /// </summary>
    public required string FileName { get; init; }
    /// <summary>
    /// The file's size, in bytes
    /// </summary>
    public required long SizeBytes { get; init; }
    /// <summary>
    /// The file's product name
    /// </summary>
    public string? ProductName { get; init; }
    /// <summary>
    /// The file's Operating system
    /// </summary>
    public string? OriginatingSystem { get; init; }
    /// <summary>
    /// The file's schema
    /// </summary>
    public string? Schema { get; init; }

    /// <summary>Name of the length unit the geometry is defined in, e.g. "millimetre".</summary>
    public string? LengthUnit { get; init; }

    /// <summary>Material density stored in the file by the CAD tool, in g/cm³, if any.</summary>
    public double? DensityGPerCm3 { get; init; }
    /// <summary>
    /// Number of entities in the file
    /// </summary>
    public required int EntityCount { get; init; }
    /// <summary>
    /// Number of solids in the file
    /// </summary>
    public required int SolidCount { get; init; }
    /// <summary>
    /// Number of faces in the file
    /// </summary>
    public required int FaceCount { get; init; }

    /// <summary>Count of each surface type used by the faces (PLANE, CYLINDRICAL_SURFACE, ...).</summary>
    public required IReadOnlyDictionary<string, int> SurfaceTypes { get; init; }
}


