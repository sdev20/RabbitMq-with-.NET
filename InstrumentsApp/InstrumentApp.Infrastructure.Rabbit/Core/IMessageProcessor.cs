namespace InstrumentApp.Infrastructure.Rabbit.Core;

public interface IMessageProcessor<in T> where T : class
{
    Task ProcessAsync(T message, CancellationToken cancellationToken);
}
