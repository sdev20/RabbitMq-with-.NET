namespace RabbitMessaging.Configuration.Models;

public class RabbitConnectionSettings
{
    public required string Name { get; set; }

    public required string Server { get; set; }

    public required string UserName { get; set; }

    public required string Password { get; set; }
}
