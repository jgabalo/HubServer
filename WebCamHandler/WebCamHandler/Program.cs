using WebCamHandler;
using WebCamHandler.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// Hub state lives in process-wide singletons, as it did under SignalR 2.
builder.Services.AddSingleton<Handler>();
builder.Services.AddSingleton<Chatter>();

// Equivalent of the previous CorsOptions.AllowAll: any origin, with credentials, which the
// SignalR JavaScript client sends by default. AllowAnyOrigin() cannot be combined with credentials.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy => policy
        .SetIsOriginAllowed(_ => true)
        .AllowAnyHeader()
        .AllowAnyMethod()
        .AllowCredentials());
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseStaticFiles();
app.UseRouting();
app.UseCors();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapHub<HandlerHub>(HubRoutes.Handler);
app.MapHub<ChatterHub>(HubRoutes.Chatter);

app.Run();

/// <summary>
/// Exposed so integration tests can host the app with WebApplicationFactory.
/// </summary>
public partial class Program { }
