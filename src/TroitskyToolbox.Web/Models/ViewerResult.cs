using TroitskyToolbox.ModelAnalysis;

namespace TroitskyToolbox.Web.Models;

/// <summary>A validated success or failure from the browser-based model viewer.</summary>
public abstract record ViewerResult
{
    private protected ViewerResult() { }

    public sealed record Success(
        StepInspection Inspection,
        MeshMeasurements Measurements,
        LoadTimings? Timings) : ViewerResult;

    public sealed record Failure(string Message) : ViewerResult;
}

/// <summary>Raw result shape returned by stepViewer.js; normalize it before using it in the UI.</summary>
public sealed record ViewerInteropResult(
    bool Ok,
    string? Error,
    StepInspection? Inspection,
    MeshMeasurements? Measurements,
    LoadTimings? Timings)
{
    public ViewerResult ToViewerResult()
    {
        if (!Ok)
            return new ViewerResult.Failure(string.IsNullOrWhiteSpace(Error) ? "The model could not be loaded." : Error);

        if (Error is not null || Inspection is null || Measurements is null)
            return new ViewerResult.Failure("The viewer returned an incomplete result.");

        return new ViewerResult.Success(Inspection, Measurements, Timings);
    }
}

/// <summary>Measurements of the tessellated model, in mm (see wwwroot/js/stepMeasure.js).</summary>
public sealed record MeshMeasurements(
    double[] Min,
    double[] Max,
    double[] Size,
    double VolumeMm3,
    double SurfaceAreaMm2,
    double[] Centroid,
    int Triangles,
    int Vertices,
    int Meshes,
    bool Closed,
    double GridCellMm);

public sealed record LoadTimings(
    double ServerMs,
    double TessellateMs,
    double MeasureMs,
    double RenderMs,
    double TotalMs);
