namespace TroitskyToolbox.ModelAnalysis;

/// <summary>
/// M7 Model Import module: validates an uploaded model before it is stored (FR-MA-1, NFR-MA-1).
/// Owns the extension check, the size limit check, the processing timeout (SR-MA-3's MA_TIMEOUT),
/// and the member-facing rejection message for each. The STEP-specific checks (header, parse,
/// geometry, metadata) are delegated to M18's <see cref="IStepParser"/>.
/// </summary>
public interface IModelImport
{
    /// <summary>
    /// Validates <paramref name="content"/> as a STEP file named <paramref name="fileName"/>.
    /// Rejects files over <paramref name="maxBytes"/> or whose extension is not .stp/.step
    /// (case-insensitive) without parsing them, then hands off to the STEP parser under a
    /// <paramref name="timeout"/> watchdog.
    /// </summary>
    Task<StepInspectionResult> ValidateAsync(
        byte[] content, string fileName, long maxBytes, TimeSpan timeout, CancellationToken ct = default);
}
