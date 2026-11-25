namespace TroitskyToolbox.ModelAnalysis;


/// <inheritdoc cref="IModelImport"/>
public sealed class ModelImport(IStepParser parser) : IModelImport
{

    private static readonly string[] AllowedExtensions = [".stp", ".step"];
    // Use decimal MB, MiB is 2^20 B / MiB
    private static int BytesPerMegabyte = 1_000_000;

    public async Task<StepInspectionResult> ValidateAsync(
        byte[] content, string fileName, long maxBytes, TimeSpan timeout, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
            return Fail(StepErrorKind.UnsupportedExtension,
                $"'{fileName}' is not a STEP file. Only .stp and .step files are accepted.");

        if (content.LongLength > maxBytes)
            
            return Fail(StepErrorKind.TooLarge,
                $"'{fileName}' is larger than the {maxBytes / BytesPerMegabyte} MB limit.");

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
