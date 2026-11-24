using System.Text;

namespace TroitskyToolbox.ModelAnalysis;

/// <summary>
/// M18 STEP Parsing module: checks the ISO-10303-21 header, parses the file, verifies it
/// contains readable geometry, and extracts the metadata the CAD tool stored in it
/// (product name, source system, units, material density) (FR-MA-1, SR-MA-3).
/// The extension check, size limit and timeout are M7's responsibility (<see cref="IModelImport"/>).
/// </summary>
public interface IStepParser
{
    /// <summary>
    /// Parses <paramref name="content"/> (already known to be within the size limit) as a STEP
    /// Part 21 file and returns its metadata, or the reason it was rejected.
    /// </summary>
    StepInspectionResult Parse(byte[] content, string fileName, CancellationToken ct = default);
}

/// <inheritdoc cref="IStepParser"/>
public sealed class StepParserService : IStepParser
{
    private static readonly string[] SolidTypes =
        ["MANIFOLD_SOLID_BREP", "BREP_WITH_VOIDS", "FACETED_BREP", "SHELL_BASED_SURFACE_MODEL"];

    private static readonly string[] FaceTypes = ["ADVANCED_FACE", "FACE_SURFACE"];

    public StepInspectionResult Parse(byte[] content, string fileName, CancellationToken ct = default)
    {
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
