namespace RabbitMessaging.Configuration.Models;

public class RabbitSchemaSettings
{
    public List<RabbitExchangeSettings> Exchanges { get; set; } = [];

    public List<RabbitQueueSettings> Queues { get; set; } = [];

    public List<RabbitBindingSettings> Bindings { get; set; } = [];
}
