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

/// <inheritdoc cref="IModelImport"/>
public sealed class ModelImport(IStepParser parser) : IModelImport
{
    private static readonly string[] AllowedExtensions = [".stp", ".step"];

    public async Task<StepInspectionResult> ValidateAsync(
        byte[] content, string fileName, long maxBytes, TimeSpan timeout, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return Fail(StepErrorKind.UnsupportedExtension,
                $"'{fileName}' is not a STEP file. Only .stp and .step files are accepted.");

        if (content.LongLength > maxBytes)
            return Fail(StepErrorKind.TooLarge,
                $"'{fileName}' is larger than the {maxBytes / (1024 * 1024)} MB limit.");

        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeoutSource.Token, ct);
        try
        {
            return await Task.Run(() => parser.Parse(content, fileName, linked.Token), linked.Token);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested)
        {
            return Fail(StepErrorKind.Timeout,
                $"Processing stopped: the time limit of {timeout.TotalSeconds:0} s was exceeded.");
        }
    }

    private static StepInspectionResult Fail(StepErrorKind kind, string message) =>
        new StepInspectionResult.Failure(new StepError(kind, message));
}
