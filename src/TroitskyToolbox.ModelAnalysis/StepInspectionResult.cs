namespace TroitskyToolbox.ModelAnalysis;

/// <summary>Result of validating/parsing a STEP file (FR-MA-1, NFR-MA-1): the metadata on success,
/// or the rejection reason on failure. Shared between M7 (<see cref="IModelImport"/>) and
/// M18 (<see cref="IStepParser"/>).</summary>
public abstract record StepInspectionResult
{
    private protected StepInspectionResult() { }
    /// <summary>
    /// Success result
    /// </summary>
    /// <param name="Inspection">The step inspection</param>
    public sealed record Success(StepInspection Inspection) : StepInspectionResult;
    /// <summary>
    /// Failure result
    /// </summary>
    /// <param name="Error">The object representing why the inspection failed</param>
    public sealed record Failure(StepError Error) : StepInspectionResult;
}
