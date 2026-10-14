using System.Text.Json;
using System.Text.Json.Serialization;

namespace CinemaAbyss.Events;

public sealed record MovieEvent
{
    [JsonPropertyName("movie_id")] public required int MovieId { get; init; }
    public required string Title { get; init; }
    public required string Action { get; init; }
    [JsonPropertyName("user_id")] public int? UserId { get; init; }
    public double? Rating { get; init; }
    public string[]? Genres { get; init; }
    public string? Description { get; init; }
}

public sealed record UserEvent
{
    [JsonPropertyName("user_id")] public required int UserId { get; init; }
    public required string Action { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    public string? Username { get; init; }
    public string? Email { get; init; }
}

public sealed record PaymentEvent
{
    [JsonPropertyName("payment_id")] public required int PaymentId { get; init; }
    [JsonPropertyName("user_id")] public required int UserId { get; init; }
    public required double Amount { get; init; }
    public required string Status { get; init; }
    public required DateTimeOffset Timestamp { get; init; }
    [JsonPropertyName("method_type")] public string? MethodType { get; init; }
}

public sealed record EventEnvelope(
    [property: JsonRequired] string Id,
    [property: JsonRequired] string Type,
    [property: JsonRequired] DateTimeOffset Timestamp,
    [property: JsonRequired] JsonElement Payload);
public sealed record EventResponse(string Status, int Partition, long Offset, EventEnvelope Event);

public static class EventJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = false,
        NumberHandling = JsonNumberHandling.Strict
    };
}
