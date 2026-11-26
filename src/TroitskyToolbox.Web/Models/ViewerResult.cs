using TroitskyToolbox.ModelAnalysis;

namespace TroitskyToolbox.Web.Models;

/// <summary>A completed or failed load from the browser-based model viewer (FR-MA-2, FR-MA-3).
/// The server already validated the file on upload (M7/M18), so this only carries the mesh
/// measurements and timings from stepViewer.js — no <c>StepInspection</c> here; that comes
/// from the selected <c>StoredModel.Inspection</c> (M8) instead.</summary>
public abstract record ViewerResult
{
    private protected ViewerResult() { }

    public sealed record Success(MeshMeasurements Measurements, LoadTimings? Timings) : ViewerResult;

    public sealed record Failure(string Message) : ViewerResult;
}

/// <summary>Raw result shape returned by stepViewer.js's <c>loadModel</c>; normalize it before
/// using it in the UI. A superseded (aborted) load normalizes to <c>null</c> — the caller must
/// ignore it rather than clearing whatever is currently shown (see stepViewer.js header comment).</summary>
public sealed record ViewerInteropResult(
    bool Ok,
    bool? Aborted,
    string? Error,
    MeshMeasurements? Measurements,
    LoadTimings? Timings)
{
    /// <summary>Null means "ignore this result" (a newer load superseded it).</summary>
    public ViewerResult? ToViewerResult()
    {
        if (Aborted == true) return null;

        if (!Ok)
            return new ViewerResult.Failure(string.IsNullOrWhiteSpace(Error) ? "The model could not be loaded." : Error);

        if (Measurements is null)
            return new ViewerResult.Failure("The viewer returned an incomplete result.");

        return new ViewerResult.Success(Measurements, Timings);
    }
}

/// <summary>Measurements of the tessellated model, in mm (see wwwroot/js/stepMeasure.js),
/// plus the grid cell size stepViewer.js chose for the scene (SR-MA-2).</summary>
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

/// <summary>Timing breakdown for one <c>loadModel</c> call (NFR-MA-2). <c>KernelMs</c> is the
/// time spent waiting for the occt WASM kernel on this call (0 once cached).</summary>
public sealed record LoadTimings(
    double DownloadMs,
    double KernelMs,
    double TessellateMs,
    double MeasureMs,
    double RenderMs,
    double TotalMs);
