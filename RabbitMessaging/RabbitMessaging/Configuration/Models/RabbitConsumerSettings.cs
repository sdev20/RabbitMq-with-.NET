namespace RabbitMessaging.Configuration.Models;

public class RabbitConsumerSettings
{
    public required string Name { get; set; }

    public required string Connection { get; set; }

    public required string Queue { get; set; }

    /// <summary>
    /// How many times RabbitConsumerHostedService will requeue a message that failed
    /// processing before dead-lettering it instead. Requires the consumer's queue to
    /// have DeadLetterExchange configured.
    /// </summary>
    public int MaxRetryCount { get; set; } = 5;
}
