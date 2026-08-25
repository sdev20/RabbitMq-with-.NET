namespace RabbitMessaging.Configuration.Models;

public class RabbitQueueSettings
{
    public required string Name { get; set; }

    public required string Connection { get; set; }

    public bool Durable { get; set; }
}
