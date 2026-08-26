namespace RabbitMessaging.Configuration.Models;

public class RabbitQueueSettings
{
    public required string Name { get; set; }

    public required string Connection { get; set; }

    public bool Durable { get; set; }

    /// <summary>
    /// Exchange to route a message to when a consumer nacks it with requeue:false
    /// (e.g. once RabbitConsumerHostedService has exhausted its retry limit).
    /// Leave null for a queue that doesn't need dead-lettering.
    /// </summary>
    public string? DeadLetterExchange { get; set; }
}
