using CinemaAbyss.Events;
using Confluent.Kafka;
using System.Globalization;
using System.Text.Json;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{builder.Configuration["PORT"] ?? "8082"}");
builder.Services.AddSingleton(new KafkaSettings(builder.Configuration));
builder.Services.AddSingleton<KafkaEventProducer>();
builder.Services.AddHostedService<KafkaEventConsumer>();

var app = builder.Build();

app.MapGet("/api/events/health", () => Results.Json(new { status = true }));
foreach (var type in new[] { "user", "payment", "movie" })
{
    app.MapPost($"/api/events/{type}", (HttpContext context, KafkaEventProducer producer) => Publish(type, context, producer));
}
app.Run();


static async Task<IResult> Publish(string type, HttpContext context, KafkaEventProducer producer)
{
    try
    {
        if (!context.Request.HasJsonContentType())
            return Results.Json(new { error = "Content-Type must be application/json." }, statusCode: 400);
        
        var payload = await JsonSerializer.DeserializeAsync<JsonElement>(context.Request.Body, EventJson.Options, context.RequestAborted);
        
        if (payload.ValueKind != JsonValueKind.Object)
            return Results.Json(new { error = "Expected a JSON object." }, statusCode: 400);

        string key;
        DateTimeOffset timestamp;
        
        switch (type)
        {
            case "user":
                var user = payload.Deserialize<UserEvent>(EventJson.Options)!;
                if (string.IsNullOrWhiteSpace(user.Action)) throw new JsonException("action is required.");
                key = user.UserId.ToString(CultureInfo.InvariantCulture);
                timestamp = user.Timestamp;
                break;
            case "payment":
                var payment = payload.Deserialize<PaymentEvent>(EventJson.Options)!;
                if (string.IsNullOrWhiteSpace(payment.Status)) throw new JsonException("status is required.");
                key = payment.PaymentId.ToString(CultureInfo.InvariantCulture);
                timestamp = payment.Timestamp;
                break;
            default:
                var movie = payload.Deserialize<MovieEvent>(EventJson.Options)!;
                if (string.IsNullOrWhiteSpace(movie.Title) || string.IsNullOrWhiteSpace(movie.Action))
                    throw new JsonException("title and action are required.");
                key = movie.MovieId.ToString(CultureInfo.InvariantCulture);
                timestamp = DateTimeOffset.UtcNow;
                break;
        }
        var envelope = new EventEnvelope(Guid.NewGuid().ToString("N"), type, timestamp, payload);
        var response = await producer.PublishAsync(envelope, key, context.RequestAborted);
        
        return Results.Json(response, EventJson.Options, statusCode: 201);
    }
    catch (JsonException)
    {
        return Results.Json(new { error = "Invalid JSON, missing required fields or invalid field types. See api-specification.yaml." }, statusCode: 400);
    }
    catch (KafkaException exception)
    {
        context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("EventsApi")
            .LogError(exception, "Event publication failed");
        return Results.Json(new { error = "Kafka publication failed; delivery may be uncertain after a timeout." }, statusCode: 500);
    }
}
