namespace RabbitMessaging.Core;

public interface IMessageProcessor<in T> where T : class
{
    Task ProcessAsync(T message, CancellationToken cancellationToken);
}
