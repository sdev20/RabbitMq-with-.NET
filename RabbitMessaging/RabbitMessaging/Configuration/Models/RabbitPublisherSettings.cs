namespace RabbitMessaging.Configuration.Models;

public class RabbitPublisherSettings
{
    public required string Name { get; set; }

    public required string Connection { get; set; }

    public required string Exchange { get; set; }
}
