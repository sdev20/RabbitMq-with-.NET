namespace Orchestration.Infrastructure.Rabbit.Core;

public interface IRabbitTopologyInitializer
{
    Task DeclareTopologyAsync();
}
