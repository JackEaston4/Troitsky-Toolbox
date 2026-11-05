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

var app = builder.Build();

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
