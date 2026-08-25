namespace RabbitMessaging.Configuration.Models;

public class RabbitExchangeSettings
{
    public required string Name { get; set; }

    public required string Connection { get; set; }

    public required string Type { get; set; }

    public bool Durable { get; set; }
}
