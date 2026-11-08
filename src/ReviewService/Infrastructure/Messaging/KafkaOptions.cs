namespace Horica.ReviewService.Infrastructure.Messaging;

public sealed class KafkaOptions
{
    public const string SectionName = "Kafka";

    /// <summary>Set to false to run the API without Kafka (events are still written to the outbox table).</summary>
    public bool Enabled { get; set; } = true;

    public string BootstrapServers { get; set; } = "localhost:9092";

    public string ClientId { get; set; } = "review-service";

    public string ConsumerGroupId { get; set; } = "review-service";

    /// <summary>Topic the service consumes (published by ClaimsService).</summary>
    public string ReviewRequestedTopic { get; set; } = "review.requested.v1";

    /// <summary>Topic the service publishes decisions to (consumed by ClaimsService).</summary>
    public string ReviewCompletedTopic { get; set; } = "review.completed.v1";

    /// <summary>Create the topics on startup if they do not exist.</summary>
    public bool EnsureTopics { get; set; } = true;

    public int TopicPartitions { get; set; } = 3;

    public short TopicReplicationFactor { get; set; } = 1;

    public int OutboxPollIntervalMs { get; set; } = 1000;

    public int OutboxBatchSize { get; set; } = 50;

    // Optional security settings (e.g. for a secured Strimzi listener or a managed Kafka).
    // SecurityProtocol: Plaintext | Ssl | SaslPlaintext | SaslSsl
    // SaslMechanism: Plain | ScramSha256 | ScramSha512 | OAuthBearer | Gssapi
    public string? SecurityProtocol { get; set; }

    public string? SaslMechanism { get; set; }

    public string? SaslUsername { get; set; }

    public string? SaslPassword { get; set; }

    /// <summary>Messages that cannot be parsed are copied here instead of blocking the partition.</summary>
    public string DeadLetterTopic => ReviewRequestedTopic + ".dlq";
}
