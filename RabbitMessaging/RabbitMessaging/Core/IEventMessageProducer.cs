namespace RabbitMessaging.Core;

public interface IEventMessageProducer<in T> where T : class
{
    Task<string> PublishAsync(T item, string routingKey);
}
