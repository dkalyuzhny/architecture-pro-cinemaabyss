using Confluent.Kafka;
using System.Text.Json;

namespace CinemaAbyss.Events;

public sealed class KafkaEventConsumer(KafkaSettings settings, ILogger<KafkaEventConsumer> logger) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) => Task.Factory.StartNew(
        () => Consume(stoppingToken), stoppingToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

    private void Consume(CancellationToken stoppingToken)
    {
        using var consumer = new ConsumerBuilder<string, string>(new ConsumerConfig
        {
            BootstrapServers = settings.Brokers,
            GroupId = settings.ConsumerGroup,
            ClientId = "cinemaabyss-events-consumer",
            AutoOffsetReset = AutoOffsetReset.Earliest,
            // Also recover quickly when topics are created after subscription.
            TopicMetadataRefreshIntervalMs = 5000,
            AllowAutoCreateTopics = true,
            SocketTimeoutMs = 10000,
            SessionTimeoutMs = 10000,
            EnableAutoCommit = true,
            EnableAutoOffsetStore = false,
            AutoCommitIntervalMs = 1000
        }).SetErrorHandler((_, error) => logger.LogWarning("Kafka consumer: {Reason}", error.Reason)).Build();
        
        consumer.Subscribe(settings.Topics.Values);
        
        logger.LogInformation("Consumer subscribed to {Topics}, group {Group}", string.Join(", ", settings.Topics.Values), settings.ConsumerGroup);
       
        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    var record = consumer.Consume(stoppingToken);
                    Process(record);
                    // Only processed records become eligible for offset commit.
                    consumer.StoreOffset(record);
                }
                catch (ConsumeException exception) when (!exception.Error.IsFatal)
                {
                    logger.LogWarning(exception, "Kafka consume failed; retrying");
                    stoppingToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(1));
                }
                catch (KafkaException exception) when (exception.Error.Code == ErrorCode.Local_State)
                {
                    // Ownership was revoked; another group member may process this record again.
                    logger.LogWarning(exception, "Partition ownership changed before offset storage");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
        finally
        {
            // Commits stored offsets and leaves the group on a normal shutdown.
            consumer.Close();
        }
    }

    private void Process(ConsumeResult<string, string> record)
    {
        try
        {
            if (record.Message.Value is null) throw new JsonException("No event payload.");
            
            var envelope = JsonSerializer.Deserialize<EventEnvelope>(record.Message.Value, EventJson.Options);
            
            if (envelope is null || string.IsNullOrWhiteSpace(envelope.Id) ||
                envelope.Type is null || !settings.Topics.TryGetValue(envelope.Type, out var topic) ||
                topic != record.Topic || envelope.Payload.ValueKind != JsonValueKind.Object)
                throw new JsonException("Invalid event envelope or topic/type mismatch.");
            
            logger.LogInformation("Consumed event {EventId}, type {Type}, topic {Topic}, partition {Partition}, offset {Offset}, payload {Payload}",
                envelope.Id, envelope.Type, record.Topic, record.Partition.Value, record.Offset.Value, envelope.Payload.GetRawText());
        }
        catch (JsonException exception)
        {
            // Explicit poison-record policy for this demo: log rejection and advance.
            logger.LogWarning(exception, "Rejected invalid record in {Topic}, partition {Partition}, offset {Offset}",
                record.Topic, record.Partition.Value, record.Offset.Value);
        }
    }
}
