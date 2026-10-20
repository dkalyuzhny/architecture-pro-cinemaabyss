using Confluent.Kafka;
using System.Text.Json;

namespace CinemaAbyss.Events;

public sealed class KafkaEventProducer : IDisposable
{
    private readonly IProducer<string, string> producer;
    private readonly KafkaSettings settings;
    private readonly ILogger<KafkaEventProducer> logger;

    public KafkaEventProducer(KafkaSettings settings, ILogger<KafkaEventProducer> logger)
    {
        this.settings = settings;
        this.logger = logger;

        producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = settings.Brokers,
            ClientId = "cinemaabyss-events-producer",
            EnableIdempotence = true,
            Acks = Acks.All,
            MessageTimeoutMs = 10000,
            RequestTimeoutMs = 5000
        }).SetErrorHandler((_, error) => logger.LogWarning("Kafka producer: {Reason}", error.Reason)).Build();
    }

    public async Task<EventResponse> PublishAsync(EventEnvelope envelope, string key, CancellationToken cancellationToken)
    {
        var topic = settings.Topics[envelope.Type];
        var delivery = await producer.ProduceAsync(topic, new Message<string, string>
        {
            Key = key,
            Value = JsonSerializer.Serialize(envelope, EventJson.Options)
        }, cancellationToken);
        
        logger.LogInformation("Published event {EventId}, type {Type}, topic {Topic}, partition {Partition}, offset {Offset}",
            envelope.Id, envelope.Type, topic, delivery.Partition.Value, delivery.Offset.Value);
        return new EventResponse("success", delivery.Partition.Value, delivery.Offset.Value, envelope);
    }

    public void Dispose()
    {
        try { producer.Flush(TimeSpan.FromSeconds(10)); }
        finally { producer.Dispose(); }
    }
}
