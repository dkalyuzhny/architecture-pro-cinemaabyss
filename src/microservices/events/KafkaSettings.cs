namespace CinemaAbyss.Events;

public sealed class KafkaSettings
{
    public string Brokers { get; }
    public string ConsumerGroup { get; }
    public IReadOnlyDictionary<string, string> Topics { get; }

    public KafkaSettings(IConfiguration configuration)
    {
        Brokers = configuration["KAFKA_BROKERS"] ?? "localhost:9092";
        ConsumerGroup = configuration["KAFKA_CONSUMER_GROUP"] ?? "cinemaabyss-events";
        
        if (string.IsNullOrWhiteSpace(Brokers) || string.IsNullOrWhiteSpace(ConsumerGroup))
            throw new InvalidOperationException("KAFKA_BROKERS and KAFKA_CONSUMER_GROUP must not be empty.");
        
        Topics = new Dictionary<string, string>
        {
            ["movie"] = configuration["KAFKA_MOVIE_TOPIC"] ?? "movie-events",
            ["user"] = configuration["KAFKA_USER_TOPIC"] ?? "user-events",
            ["payment"] = configuration["KAFKA_PAYMENT_TOPIC"] ?? "payment-events"
        };
        
        if (Topics.Values.Any(string.IsNullOrWhiteSpace) || Topics.Values.Distinct().Count() != 3)
            throw new InvalidOperationException("Configure three Kafka topic names.");
    }
}
