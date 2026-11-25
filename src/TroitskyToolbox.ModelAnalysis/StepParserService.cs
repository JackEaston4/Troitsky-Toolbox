using System.Reflection.Metadata.Ecma335;
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

    /// <summary>
    /// Display of STEP opener in case of an error
    /// </summary>
    private static readonly string StepOpenerDisplayed = StepParser.StepOpener[..^1];

    /// <summary>
    /// STEP surface name suffixes (try more specific one first)
    /// </summary>
    private static readonly string[] StepSurfaceNameSuffixes = { "_SURFACE_WITH_KNOTS", "SURFACE" };
    /// <summary>
    /// The index of the Operating System (OS) in a STEP document's file name
    /// </summary>
    private const int StepFileNameOsIdx = 5;
    /// <summary>
    /// The STEP document type for a product
    /// </summary>
    private const string ProductType = "PRODUCT";

    public StepInspectionResult Parse(byte[] content, string fileName, CancellationToken ct = default)
    {
        var text = Encoding.Latin1.GetString(content);
        if (!text.TrimStart().StartsWith(StepParser.StepOpener, StringComparison.Ordinal))
        {
            // Omit ; from error message
            return Fail(StepErrorKind.NotStep,
                $"'{fileName}' is not a STEP file: it does not start with the '{StepOpenerDisplayed} header.");
        }
            

        StepDocument doc;
        try
        {
            doc = StepParser.Parse(text, ct);
        }
        catch (StepFormatException ex)
        {
            return Fail(StepErrorKind.Malformed, $"'{fileName}' is not a valid STEP file. {ex.Message}");
        }

        IEnumerable<StepEntity> entities = doc.Entities.Values;
        int numSolids = entities.Count(e => SolidTypes.Any(e.Is));
        IEnumerable<StepEntity> faces = entities.Where(e => FaceTypes.Any(e.Is));
        if (numSolids == 0 && !faces.Any())
        {
            return Fail(StepErrorKind.NoGeometry, $"'{fileName}' contains no readable geometry (no solids or faces).");
        }
            

        var surfaceTypes = faces
            .Select(f => doc.Get(f.Parts[0].Args.ElementAtOrDefault(2)))
            .Where(s => s?.Parts.Count > 0)
            .Select(s => s!.Parts.Count == 1 ? s.Parts[0].Name : SurfaceName(s))
            .GroupBy(n => n)
            .OrderByDescending(g => g.Count())
            .ToDictionary(g => g.Key, g => g.Count());

        return new StepInspectionResult.Success(new StepInspection
        {
            FileName = fileName,
            SizeBytes = content.LongLength,
            ProductName = ProductName(doc),
            OriginatingSystem = GetOperatingSystem(doc),
            Schema = (doc.FileSchema[0] as IEnumerable<string>)?.FirstOrDefault(),
            LengthUnit = StepUnits.GeometryLengthUnit(doc),
            DensityGPerCm3 = StepUnits.Density(doc),
            EntityCount = doc.Entities.Count,
            SolidCount = numSolids,
            FaceCount = faces.Count(),
            SurfaceTypes = surfaceTypes,
        });
    }

    /// <summary>
    /// Get the operating system of a document
    /// </summary>
    /// <param name="doc">The STEP document</param>
    /// <returns>The found operating system</returns>
    private static string? GetOperatingSystem(StepDocument doc)
    {
        var osVal = (string?) doc.FileName.ElementAtOrDefault(StepFileNameOsIdx);
        return osVal is not null ? osVal : null;
    }

    /// <summary>For complex surface instances (e.g. B-spline variants), use the most specific part name.</summary>
    private static string SurfaceName(StepEntity s)
    {
        IEnumerable<string> names = s.Parts.Select(p => p.Name);
        foreach (string suffix in StepSurfaceNameSuffixes)
        {
            string? name = names.Where(n => n.EndsWith(suffix, StringComparison.Ordinal)).FirstOrDefault();
            if (name is not null) return name;
        }
        return names.First();
    }

    private static string? ProductName(StepDocument doc)
    {
        StepTyped? product = doc.Entities.Values.FirstOrDefault(e => e.Is(ProductType))?.Part(ProductType);
        var name = product?.Args.ElementAtOrDefault(1) as string;
        if (string.IsNullOrWhiteSpace(name)) name = product?.Args.ElementAtOrDefault(0) as string;
        return string.IsNullOrWhiteSpace(name) ? null : name;
    }

    /// <summary>
    /// Create a new STEP Inspection Failure result
    /// </summary>
    /// <param name="kind">The error kind</param>
    /// <param name="message">The error message</param>
    /// <returns>The corresponding failure result</returns>
    private static StepInspectionResult Fail(StepErrorKind kind, string message)
    {
        return new StepInspectionResult.Failure(new StepError(kind, message));
    }
        
}
