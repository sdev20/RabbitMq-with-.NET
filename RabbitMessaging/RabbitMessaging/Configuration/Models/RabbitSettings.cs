namespace RabbitMessaging.Configuration.Models;

public class RabbitSettings
{
    public required List<RabbitConnectionSettings> Connections { get; set; }

    public required List<RabbitPublisherSettings> Publishers { get; set; }

    public required RabbitSchemaSettings Schema { get; set; }

    public List<RabbitConsumerSettings> Consumers { get; set; } = [];
}
