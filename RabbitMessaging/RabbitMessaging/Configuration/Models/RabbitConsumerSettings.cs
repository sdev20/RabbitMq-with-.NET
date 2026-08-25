namespace RabbitMessaging.Configuration.Models;

public class RabbitConsumerSettings
{
    public required string Name { get; set; }

    public required string Connection { get; set; }

    public required string Queue { get; set; }
}
