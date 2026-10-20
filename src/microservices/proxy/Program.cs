using System.Diagnostics;
using System.Net;
using CinemaAbyss.Proxy;
using Yarp.ReverseProxy.Forwarder;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{builder.Configuration["PORT"] ?? "8000"}");


var aliases = new Dictionary<string, string?>();
if (builder.Configuration["GRADUAL_MIGRATION"] is { } enabled)
    aliases["Migration:Enabled"] = enabled;
if (builder.Configuration["MOVIES_MIGRATION_PERCENT"] is { } percent)
    aliases["Migration:MoviesPercent"] = percent;
builder.Configuration.AddInMemoryCollection(aliases);


builder.Services.AddHttpForwarder();
builder.Services.AddSingleton<MigrationFlag>();

var monolith = Destination("MONOLITH_URL", "http://localhost:8080");
var movies = Destination("MOVIES_SERVICE_URL", "http://localhost:8081");
var events = builder.Configuration["EVENTS_SERVICE_URL"] is { Length: > 0 } eventUrl
    ? ValidateDestination(eventUrl) : null;

using var client = new HttpMessageInvoker(new SocketsHttpHandler
{
    UseProxy = false,
    AllowAutoRedirect = false,
    AutomaticDecompression = DecompressionMethods.None,
    UseCookies = false,
    ConnectTimeout = TimeSpan.FromSeconds(5),
    PooledConnectionLifetime = TimeSpan.FromMinutes(5),
    ActivityHeadersPropagator = new ReverseProxyPropagator(DistributedContextPropagator.Current)
});

var requestConfig = new ForwarderRequestConfig { ActivityTimeout = TimeSpan.FromSeconds(30) };
var app = builder.Build();
var flag = app.Services.GetRequiredService<MigrationFlag>(); // validate at startup

app.MapGet("/health", () => Results.Json(new { status = true }));

app.Map("/{**catchAll}", async (HttpContext context) =>
{
    var path = context.Request.Path;
    var target = "monolith";
    var destination = monolith;
    if (path.StartsWithSegments("/api/movies", StringComparison.OrdinalIgnoreCase))
    {
        // Microservice health is not a legacy business endpoint.
        if (path.Equals(new PathString("/api/movies/health")) || flag.UseMovies())
        {
            target = "movies";
            destination = movies;
        }
    }
    else if (events is not null && path.StartsWithSegments("/api/events", StringComparison.OrdinalIgnoreCase))
    {
        target = "events";
        destination = events;
    }

    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Proxy-Target"] = target;
        return Task.CompletedTask;
    });
    var forwarder = context.RequestServices.GetRequiredService<IHttpForwarder>();
    var error = await forwarder.SendAsync(context, destination, client, requestConfig, HttpTransformer.Default);
    app.Logger.LogInformation("Proxy {Method} {Path} -> {Target}, status {Status}, error {Error}",
        context.Request.Method, path, target, context.Response.StatusCode, error);

});
app.Run();

string Destination(string key, string fallback) => ValidateDestination(builder.Configuration[key] ?? fallback);
static string ValidateDestination(string value)
{
    if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
        (uri.Scheme != "http" && uri.Scheme != "https") ||
        uri.AbsolutePath != "/" || uri.Query.Length != 0 || uri.Fragment.Length != 0 || uri.UserInfo.Length != 0)
        throw new InvalidOperationException("Backend URL must be an HTTP(S) origin without a path, credentials, query or fragment.");
    return uri.GetLeftPart(UriPartial.Authority) + "/";
}
