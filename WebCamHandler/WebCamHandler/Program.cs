using Microsoft.Extensions.Options;
using TradeFeed.Contracts;
using WebCamHandler;
using WebCamHandler.Hubs;
using WebCamHandler.Trading;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddSignalR();

// Hub state lives in process-wide singletons, as it did under SignalR 2.
builder.Services.AddSingleton<Handler>();
builder.Services.AddSingleton<Chatter>();

// Live trade relay: publishers push batches, viewers receive them read-only.
builder.Services.Configure<TradeFeedOptions>(builder.Configuration.GetSection(TradeFeedOptions.SectionName));
builder.Services.AddSingleton<TradeStore>();

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
app.MapHub<TradeHub>(TradeHubContract.Route);

if (string.IsNullOrEmpty(app.Services.GetRequiredService<IOptions<TradeFeedOptions>>().Value.PublisherKey))
{
    app.Logger.LogWarning("TradeFeed:PublisherKey is not set, so trade publishing is disabled.");
}

app.Run();

/// <summary>
/// Exposed so integration tests can host the app with WebApplicationFactory.
/// </summary>
public partial class Program { }
