using TroitskyToolbox.Web;
using TroitskyToolbox.Web.Components;
using TroitskyToolbox.Web.Endpoints;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents()
    .AddCircuitOptions(o => o.JSInteropDefaultCallTimeout = TimeSpan.FromMinutes(3));

var modelSection = builder.Configuration.GetSection(ModelAnalysisOptions.Section);
builder.Services.Configure<ModelAnalysisOptions>(modelSection);
var maxUpload = modelSection.Get<ModelAnalysisOptions>()?.MaxUploadBytes ?? new ModelAnalysisOptions().MaxUploadBytes;
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = maxUpload + 1024);

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
