using TroitskyToolbox.ModelAnalysis;

namespace TroitskyToolbox.Web.Models;

/// <summary>What stepViewer.js returns after loading a model.</summary>
public sealed record ViewerResult(
    bool Ok,
    string? Error,
    StepInspection? Inspection,
    MeshMeasurements? Measurements,
    LoadTimings? Timings);

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
