using System.Reflection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Platform.Api.Admin;
using Platform.Api.Auth;
using Platform.Api.Feedback;
using Platform.Api.Landing;
using Platform.Api.Observability;
using Platform.Api.Projects;
using Platform.Api.Storage;
using Platform.Application;
using Platform.Contracts.Http;
using Platform.Infrastructure;
using Platform.Infrastructure.Observability;
using Platform.Realtime;
using Platform.Realtime.Diagnostics;

var builder = WebApplication.CreateBuilder(args);

// Capture recent logs in memory for the dashboard, alongside console logging.
var recentLogStore = new RecentLogStore();
builder.Services.AddSingleton(recentLogStore);

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.SingleLine = true;
    options.TimestampFormat = "HH:mm:ss ";
});
builder.Logging.AddProvider(new RecentLogProvider(recentLogStore));

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddRealtime(builder.Configuration);
builder.Services.AddDashboardAuth();
builder.Services.AddScoped<Platform.Dashboard.DashboardUser>();
builder.Services.AddRazorComponents();

// Basic OpenTelemetry: publish our realtime meter + ASP.NET Core metrics.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("SpawnWeaver.Platform"))
    .WithMetrics(metrics =>
    {
        metrics.AddMeter(RealtimeMetrics.MeterName);
        metrics.AddAspNetCoreInstrumentation();
        if (!builder.Environment.IsEnvironment("Testing"))
        {
            metrics.AddConsoleExporter();
        }
    });

var app = builder.Build();

// Serve the shared design-system stylesheet and other static assets from wwwroot.
app.UseStaticFiles();

// Tag every request with a correlation id (response header + logging scope).
app.UseMiddleware<CorrelationIdMiddleware>();

// Enable WebSockets with a keep-alive interval that drives ping/pong heartbeats.
app.UseWebSockets(new WebSocketOptions
{
    KeepAliveInterval = TimeSpan.FromSeconds(30),
});

// Prepare the control-plane database on startup (migrate for SQLite, create for Postgres).
app.Services.InitializePlatformDatabase();

// Package the Godot SDK addon into a downloadable zip for the one-line installer.
Platform.Api.Sdk.SdkPackaging.EnsureSdkPackage(
    app.Environment, app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("SdkPackaging"));

var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "0.0.0";

app.MapGet("/", (HttpContext ctx) => Results.Content(
    LandingPage.Render(ctx.User.Identity?.IsAuthenticated ?? false, ctx.User.Identity?.Name), "text/html"));

// One-line SDK installers. Served dynamically so the embedded base URL matches this origin
// (or App:PublicBaseUrl). The scripts download /sdk/multiplayer_service.zip and extract it
// into the current Godot project's addons/ folder.
app.MapGet("/install.ps1", (HttpContext ctx, IWebHostEnvironment env) =>
    ServeInstallScript(ctx, env, "install.ps1"));
app.MapGet("/install.sh", (HttpContext ctx, IWebHostEnvironment env) =>
    ServeInstallScript(ctx, env, "install.sh"));

// Liveness/readiness probe used by Docker, tests, and tunnels.
app.MapGet("/health", () =>
    Results.Ok(new HealthResponse(Status: "ok", Service: "Platform.Api", Version: version)));

app.UseAuthentication();
app.UseAuthorization();

// Redirect logged-out visitors away from protected dashboard pages.
app.UseMiddleware<DashboardGuardMiddleware>();

app.UseAntiforgery();

app.MapAuthEndpoints();
app.MapProjectEndpoints();
app.MapStorageEndpoints();
app.MapFeedbackEndpoints();
app.MapRealtimeEndpoints();
app.MapAdminEndpoints();

// Blazor (static SSR) admin dashboard at /dashboard.
app.MapRazorComponents<Platform.Dashboard.Components.App>();

// One-time startup log; CA1848 (LoggerMessage delegates) is for hot paths, not this.
#pragma warning disable CA1848
app.Logger.LogInformation("Platform.Api started (version {Version})", version);
#pragma warning restore CA1848

app.Run();

// Serves an install script from wwwroot with its __BASE_URL__ placeholder replaced by this
// deployment's public base URL (App:PublicBaseUrl) or the incoming request origin.
IResult ServeInstallScript(HttpContext ctx, IWebHostEnvironment env, string fileName)
{
    var baseUrl = app.Configuration["App:PublicBaseUrl"];
    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host}";
    }

    var path = Path.Combine(env.WebRootPath ?? Path.Combine(env.ContentRootPath, "wwwroot"), fileName);
    if (!File.Exists(path))
    {
        return Results.NotFound();
    }

    var content = File.ReadAllText(path).Replace("__BASE_URL__", baseUrl.TrimEnd('/'), StringComparison.Ordinal);
    return Results.Text(content, "text/plain; charset=utf-8");
}

// Exposed so Platform.Tests can spin up the API via WebApplicationFactory<Program>.
public partial class Program;
