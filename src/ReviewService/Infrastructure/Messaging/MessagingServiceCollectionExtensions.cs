using Confluent.Kafka;

namespace Horica.ReviewService.Infrastructure.Messaging;

public static class MessagingServiceCollectionExtensions
{
    public static IServiceCollection AddReviewMessaging(this IServiceCollection services, IConfiguration configuration)
    {
        // Options are always registered: the API needs the topic names to write outbox rows even when Kafka is disabled.
        services.Configure<KafkaOptions>(configuration.GetSection(KafkaOptions.SectionName));
        services.Configure<EventOptions>(configuration.GetSection(EventOptions.SectionName));

        var kafka = configuration.GetSection(KafkaOptions.SectionName).Get<KafkaOptions>() ?? new KafkaOptions();
        if (!kafka.Enabled)
        {
            return services;
        }

        services.AddSingleton<KafkaTopicInitializer>();

        services.AddSingleton<IProducer<string, string>>(provider =>
        {
            var options = provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<KafkaOptions>>().Value;
            var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger("Kafka.Producer");

            return new ProducerBuilder<string, string>(KafkaConfigFactory.CreateProducerConfig(options))
                .SetErrorHandler((_, error) => logger.LogWarning("Kafka producer: {Reason}", error.Reason))
                .Build();
        });

        services.AddHostedService<OutboxPublisher>();
        services.AddHostedService<ReviewRequestedConsumer>();

        return services;
    }
}
