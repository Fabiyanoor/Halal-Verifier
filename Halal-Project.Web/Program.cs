using Authentication;
using Blazored.Toast;
using Halal_Project.Web;
using Halal_Project.Web.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;

var builder = WebApplication.CreateBuilder(args);

// Add service defaults & Aspire components.
builder.AddServiceDefaults();

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddBlazoredToast();

builder.Services.AddAuthenticationCore();
builder.Services.AddCascadingAuthenticationState();

builder.Services.AddOutputCache();
builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();

builder.Services.AddHttpClient<ApiClient>(client =>
{
    // This URL uses "https+http://" to indicate HTTPS is preferred over HTTP.
    // Learn more about service discovery scheme resolution at https://aka.ms/dotnet/sdschemes.
    client.BaseAddress = new("https+https://localhost:7507");
});
builder.Services.AddHttpClient("HalalAPI", client =>
{
    // Set the base address to the application's base URL
    // Adjust the port if necessary based on your launchSettings.json or hosting environment
    client.BaseAddress = new Uri(builder.Configuration["ApiBaseUrl"] ?? "https://localhost:7507/"); // Example port
}).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    // Optional: Handle SSL issues in development
    ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => true
});


builder.Services.AddScoped<AuthenticationStateProvider, CustomAuthStateProvider>();
builder.Services.AddScoped<CustomAuthStateProvider>();
builder.Services.AddBlazoredToast(); // If using toast notifications
builder.Services.AddScoped<ProtectedLocalStorage>();// Add localization services
builder.Services.AddLocalization();
builder.Services.AddControllers();

var app = builder.Build();
app.UseStaticFiles(); // This should be before app.UseRouting()
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
var supportedCultures = new[] { "en-US", "fr-FR" };
var localizeoptions = new RequestLocalizationOptions()
    .SetDefaultCulture("en-US")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizeoptions);

app.UseHttpsRedirection();

app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.UseOutputCache();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.MapDefaultEndpoints();
app.MapControllers();

app.Run();
