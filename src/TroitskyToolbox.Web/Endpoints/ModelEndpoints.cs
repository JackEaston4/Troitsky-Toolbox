using Microsoft.Extensions.Options;
using TroitskyToolbox.ModelAnalysis;

namespace TroitskyToolbox.Web.Endpoints;

/// <summary>HTTP API for the Model Analysis proof of concept.</summary>
public static class ModelEndpoints
{
    public static void MapModelEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        // Validates a STEP file and returns its metadata (FR-MA-1, NFR-MA-1, SR-MA-3).
        // The body is the raw file; the file name comes from ?name= so the extension can be checked.
        api.MapPost("/models/inspect", async (HttpRequest request, string name, IOptions<ModelAnalysisOptions> options) =>
        {
            var opts = options.Value;
            if (request.ContentLength > opts.MaxUploadBytes)
                return TooLarge(opts);

            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(opts.TimeoutSeconds));
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(timeout.Token, request.HttpContext.RequestAborted);

            byte[] content;
            try
            {
                using var buffer = new MemoryStream();
                await request.Body.CopyToAsync(buffer, linked.Token);
                if (buffer.Length > opts.MaxUploadBytes) return TooLarge(opts);
                content = buffer.ToArray();

                var result = await Task.Run(() => StepInspector.Inspect(content, Path.GetFileName(name), linked.Token), linked.Token);
                return result switch
                {
                    StepInspectionResult.Success success => Results.Ok(success.Inspection),
                    StepInspectionResult.Failure failure => Problem(failure.Error),
                    _ => throw new InvalidOperationException("Unknown STEP inspection result.")
                };
            }
            catch (OperationCanceledException) when (timeout.IsCancellationRequested)
            {
                return Problem(new StepError(StepErrorKind.Timeout,
                    $"Processing stopped: the time limit of {opts.TimeoutSeconds} s was exceeded."));
            }
        });

        // Sample files for demos (src/sample_step_files).
        api.MapGet("/samples", (IOptions<ModelAnalysisOptions> options, IWebHostEnvironment env) =>
            Results.Ok(SampleFiles(options.Value, env).Select(Path.GetFileName)));

        api.MapGet("/samples/{name}", (string name, IOptions<ModelAnalysisOptions> options, IWebHostEnvironment env) =>
        {
            // Only serve files that are listed, so the name cannot escape the folder.
            var path = SampleFiles(options.Value, env).FirstOrDefault(p => Path.GetFileName(p) == name);
            return path is null ? Results.NotFound() : Results.File(path, "application/step", name);
        });
    }

    public static IEnumerable<string> SampleFiles(ModelAnalysisOptions opts, IWebHostEnvironment env)
    {
        var folder = Path.GetFullPath(Path.Combine(env.ContentRootPath, opts.SampleFolder));
        if (!Directory.Exists(folder)) return [];
        return Directory.EnumerateFiles(folder)
            .Where(f => Path.GetExtension(f).ToLowerInvariant() is ".stp" or ".step")
            .Order();
    }

    private static IResult TooLarge(ModelAnalysisOptions opts) =>
        Problem(new StepError(StepErrorKind.TooLarge, $"The file is larger than the {opts.MaxUploadBytes / (1024 * 1024)} MB limit."));

    private static IResult Problem(StepError error) => Results.Json(
        new { kind = error.Kind.ToString(), message = error.Message },
        statusCode: error.Kind switch
        {
            StepErrorKind.UnsupportedExtension => StatusCodes.Status415UnsupportedMediaType,
            StepErrorKind.TooLarge => StatusCodes.Status413PayloadTooLarge,
            StepErrorKind.Timeout => StatusCodes.Status408RequestTimeout,
            _ => StatusCodes.Status400BadRequest,
        });
}
