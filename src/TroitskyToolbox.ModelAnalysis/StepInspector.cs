using System.Text;

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

public abstract record StepInspectionResult
{
    private protected StepInspectionResult() { }

    public sealed record Success(StepInspection Inspection) : StepInspectionResult;
    public sealed record Failure(StepError Error) : StepInspectionResult;
}

/// <summary>
/// Validates an uploaded model (FR-MA-1, NFR-MA-1) and reads the metadata the CAD
/// tool stored in it: product name, source software, units and material density.
/// </summary>
public static class StepInspector
{
    private static readonly string[] AllowedExtensions = [".stp", ".step"];

    private static readonly string[] SolidTypes =
        ["MANIFOLD_SOLID_BREP", "BREP_WITH_VOIDS", "FACETED_BREP", "SHELL_BASED_SURFACE_MODEL"];

    private static readonly string[] FaceTypes = ["ADVANCED_FACE", "FACE_SURFACE"];

    public static StepInspectionResult Inspect(byte[] content, string fileName, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return Fail(StepErrorKind.UnsupportedExtension,
                $"'{fileName}' is not a STEP file. Only .stp and .step files are accepted.");

        var text = Encoding.Latin1.GetString(content);
        if (!text.TrimStart().StartsWith(StepParser.Magic, StringComparison.Ordinal))
            return Fail(StepErrorKind.NotStep,
                $"'{fileName}' is not a STEP file: it does not start with the ISO-10303-21 header.");

        StepDocument doc;
        try
        {
            doc = StepParser.Parse(text, ct);
        }
        catch (StepFormatException ex)
        {
            return Fail(StepErrorKind.Malformed, $"'{fileName}' is not a valid STEP file. {ex.Message}");
        }

        var entities = doc.Entities.Values;
        var solids = entities.Count(e => SolidTypes.Any(e.Is));
        var faces = entities.Where(e => FaceTypes.Any(e.Is)).ToList();
        if (solids == 0 && faces.Count == 0)
            return Fail(StepErrorKind.NoGeometry, $"'{fileName}' contains no readable geometry (no solids or faces).");

        var surfaceTypes = faces
            .Select(f => doc.Get(f.Parts[0].Args.ElementAtOrDefault(2)))
            .Where(s => s is not null)
            .Select(s => s!.Parts.Count == 1 ? s.Parts[0].Name : SurfaceName(s))
            .GroupBy(n => n)
            .OrderByDescending(g => g.Count())
            .ToDictionary(g => g.Key, g => g.Count());

        return new StepInspectionResult.Success(new StepInspection
        {
            FileName = fileName,
            SizeBytes = content.LongLength,
            ProductName = ProductName(doc),
            OriginatingSystem = doc.FileName.ElementAtOrDefault(5) as string is { Length: > 0 } sys ? sys : null,
            Schema = (doc.FileSchema.ElementAtOrDefault(0) as IReadOnlyList<object?>)?.FirstOrDefault() as string,
            LengthUnit = StepUnits.GeometryLengthUnit(doc),
            DensityGPerCm3 = StepUnits.Density(doc),
            EntityCount = doc.Entities.Count,
            SolidCount = solids,
            FaceCount = faces.Count,
            SurfaceTypes = surfaceTypes,
        });
    }

    /// <summary>For complex surface instances (e.g. B-spline variants), use the most specific part name.</summary>
    private static string SurfaceName(StepEntity s) =>
        s.Parts.Select(p => p.Name).FirstOrDefault(n => n.EndsWith("_SURFACE_WITH_KNOTS", StringComparison.Ordinal))
        ?? s.Parts.Select(p => p.Name).FirstOrDefault(n => n.EndsWith("SURFACE", StringComparison.Ordinal))
        ?? s.Parts[0].Name;

    private static string? ProductName(StepDocument doc)
    {
        var product = doc.Entities.Values.FirstOrDefault(e => e.Is("PRODUCT"))?.Part("PRODUCT");
        var name = product?.Args.ElementAtOrDefault(1) as string;
        if (string.IsNullOrWhiteSpace(name)) name = product?.Args.ElementAtOrDefault(0) as string;
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    private static StepInspectionResult Fail(StepErrorKind kind, string message) =>
        new StepInspectionResult.Failure(new StepError(kind, message));
}
