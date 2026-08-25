namespace RabbitMessaging.Core;

public interface IRabbitTopologyInitializer
{
    Task DeclareTopologyAsync();
}
