namespace Orchestration.Infrastructure.Rabbit.Models;

public class RabbitSettings
{
    public required List<RabbitConnectionSettings> Connections { get; set; }

    public required List<RabbitPublisherSettings> Publishers { get; set; }

    public required RabbitSchemaSettings Schema { get; set; }

    public List<RabbitConsumerSettings> Consumers { get; set; } = [];
}

public class RabbitConnectionSettings
{
    public required string Name { get; set; }

    public required string Server { get; set; }

    public required string UserName { get; set; }

    public required string Password { get; set; }
}

public class RabbitPublisherSettings
{
    public required string Name { get; set; }

    public required string Connection { get; set; }

    public required string Exchange { get; set; }
}

public class RabbitSchemaSettings
{
    public List<RabbitExchangeSettings> Exchanges { get; set; } = [];

    public List<RabbitQueueSettings> Queues { get; set; } = [];

    public List<RabbitBindingSettings> Bindings { get; set; } = [];
}

public class RabbitExchangeSettings
{
    public required string Name { get; set; }

    public required string Connection { get; set; }

    public required string Type { get; set; }

    public bool Durable { get; set; }
}

public class RabbitQueueSettings
{
    public required string Name { get; set; }

    public required string Connection { get; set; }

    public bool Durable { get; set; }
}

public class RabbitBindingSettings
{
    public required string Exchange { get; set; }

    public required string Queue { get; set; }

    public required string RoutingKey { get; set; }
}

public class RabbitConsumerSettings
{
    public required string Name { get; set; }

    public required string Connection { get; set; }

    public required string Queue { get; set; }
}
