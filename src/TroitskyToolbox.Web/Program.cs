using TroitskyToolbox.ModelAnalysis;
using TroitskyToolbox.Storage;
using TroitskyToolbox.Web;
using TroitskyToolbox.Web.Components;
using TroitskyToolbox.Web.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddCircuitOptions(o => o.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(3));

var modelSection = builder.Configuration.GetSection(ModelAnalysisOptions.Section);
builder.Services.AddOptions<ModelAnalysisOptions>()
    .Bind(modelSection)
    .ValidateDataAnnotations()
    .ValidateOnStart();
var modelOptions = modelSection.Get<ModelAnalysisOptions>() ?? new ModelAnalysisOptions();
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = checked(modelOptions.MaxUploadBytes + 1024));

// M18 STEP parsing, M7 model import validation (FR-MA-1, NFR-MA-1).
builder.Services.AddSingleton<IStepParser, StepParserService>();
builder.Services.AddSingleton<IModelImport, ModelImport>();

// M16 persistence + M8 model library (FR-KR-1, FR-KR-2, FR-MA-4 partial).
builder.Services.AddSingleton<IPersistence>(sp =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ModelAnalysisOptions>>().Value;
    var root = Path.GetFullPath(Path.Combine(env.ContentRootPath, opts.StorageRoot));
    return new FilePersistence(root);
});
builder.Services.AddSingleton(sp =>
{
    var env = sp.GetRequiredService<IWebHostEnvironment>();
    var opts = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<ModelAnalysisOptions>>().Value;
    var sampleFolder = Path.GetFullPath(Path.Combine(env.ContentRootPath, opts.SampleFolder));
    return new ModelLibrary(
        sp.GetRequiredService<IPersistence>(),
        sp.GetRequiredService<IModelImport>(),
        opts.MaxUploadBytes,
        TimeSpan.FromSeconds(opts.TimeoutSeconds),
        sampleFolder);
});
builder.Services.AddSingleton<IModelLibrary>(sp => sp.GetRequiredService<ModelLibrary>());

var app = builder.Build();

// Loads index.json (or seeds it from SampleFolder if empty) once at startup.
await app.Services.GetRequiredService<ModelLibrary>().InitializeAsync();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapModelEndpoints();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
