using Microsoft.Extensions.Options;
using TroitskyToolbox.ModelAnalysis;
using TroitskyToolbox.Storage;

namespace TroitskyToolbox.Web.Endpoints;

/// <summary>HTTP API for the model library (FR-KR-1, FR-KR-2): upload, list, download.</summary>
public static class ModelEndpoints
{
    private const int BPerMb = 1_048_576; // 1024 * 1024
    public static void MapModelEndpoints(this IEndpointRouteBuilder app)
    {
        var api = app.MapGroup("/api");

        // Upload + validate (M7/M18) + store (M8/M16). Raw body avoids the antiforgery
        // requirement .NET 8+ puts on IFormFile endpoints. FR-MA-1, NFR-MA-1, SR-MA-3.
        api.MapPost("/models", async (HttpRequest request, string name, IModelLibrary library,
            IOptions<ModelAnalysisOptions> options) =>
        {
            var opts = options.Value;
            if (request.ContentLength is { } declaredLength && declaredLength > opts.MaxUploadBytes)
                return TooLarge(opts);

            var result = await library.UploadModelAsync(request.Body, name, request.HttpContext.RequestAborted);
            return result switch
            {
                UploadResult.Success success => Results.Json(ToJson(success.Model, success.AlreadyStored),
                    statusCode: success.AlreadyStored ? StatusCodes.Status200OK : StatusCodes.Status201Created),
                UploadResult.Failure failure => Problem(failure.Error),
                _ => throw new InvalidOperationException("Unknown upload result."),
            };
        });

        // List of stored models for the explorer / curl debugging. FR-KR-1.
        api.MapGet("/models", async (IModelLibrary library) =>
        {
            var models = await library.ListModelsAsync();
            return Results.Ok(models.Select(m => ToJson(m)));
        });

        // Bytes for the browser-side viewer (occt-import-js); ?download=1 adds an attachment
        // disposition with the original file name for the Download button. FR-KR-1.
        api.MapGet("/models/{id:guid}/content", async (Guid id, int? download, IModelLibrary library) =>
        {
            var model = await library.GetModelAsync(id);
            if (model is null) return Results.NotFound();

            var stream = await library.OpenContentAsync(id);
            if (stream is null) return Results.NotFound();

            return download == 1
                ? Results.File(stream, "application/step", model.FileName)
                : Results.File(stream, "application/step");
        });
    }

    private static object ToJson(StoredModel m, bool? alreadyStored = null) => alreadyStored is null
        ? new { id = m.Id, fileName = m.FileName, sizeBytes = m.SizeBytes, uploadedAt = m.UploadedAt }
        : new { id = m.Id, fileName = m.FileName, sizeBytes = m.SizeBytes, uploadedAt = m.UploadedAt, alreadyStored = alreadyStored.Value };

    private static IResult TooLarge(ModelAnalysisOptions opts) =>
        Problem(new StepError(StepErrorKind.TooLarge, $"The file is larger than the {opts.MaxUploadBytes / BPerMb} MB limit."));

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
