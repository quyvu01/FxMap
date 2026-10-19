namespace FxMap.RabbitMq.Registries;

internal sealed class RabbitMqConfigurator : IRabbitMqConfigurator
{
    internal string HostValue { get; private set; }
    internal string VirtualHostValue { get; private set; }
    internal int PortValue { get; private set; } = 5672;
    internal int ChannelPoolSizeValue { get; private set; } = 5;
    internal RabbitMqCredential Credential { get; } = new();

    public void Host(string host, string virtualHost, int port = 5672, Action<RabbitMqCredential> configure = null)
    {
        HostValue = host;
        VirtualHostValue = virtualHost;
        PortValue = port;
        configure?.Invoke(Credential);
    }

    /// <summary>
    /// Number of channels each transport component (request client, server) keeps open
    /// concurrently over the shared connection. Defaults to 5.
    /// </summary>
    public void ChannelPoolSize(int size)
    {
        // A pool with no channel could never lend one: the requests would wait for ever
        ArgumentOutOfRangeException.ThrowIfLessThan(size, 1);
        ChannelPoolSizeValue = size;
    }
}
