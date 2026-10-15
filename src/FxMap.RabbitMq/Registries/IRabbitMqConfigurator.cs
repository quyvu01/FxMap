namespace FxMap.RabbitMq.Registries;

public interface IRabbitMqConfigurator
{
    void Host(string host, string virtualHost, int port = 5672, Action<RabbitMqCredential> configure = null);

    /// <summary>
    /// Number of channels each transport component (request client, server) keeps open
    /// concurrently over the shared connection. Defaults to 5.
    /// </summary>
    void ChannelPoolSize(int size);
}
