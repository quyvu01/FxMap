using FxMap.RabbitMq.Statics;

namespace FxMap.RabbitMq.Registries;

public sealed class RabbitMqConfigurator
{
    public void Host(string host, string virtualHost, int port = 5672, Action<RabbitMqCredential> configure = null)
    {
        (RabbitMqStatics.RabbitMqHost, RabbitMqStatics.RabbitVirtualHost, RabbitMqStatics.RabbitMqPort) =
            (host, virtualHost, port);
        var rabbitMqCredential = new RabbitMqCredential();
        configure?.Invoke(rabbitMqCredential);
    }
}