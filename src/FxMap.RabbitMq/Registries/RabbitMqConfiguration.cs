using RabbitMQ.Client;

namespace FxMap.RabbitMq.Registries;

internal sealed class RabbitMqConfiguration(
    string rabbitMqHost,
    string rabbitVirtualHost,
    int rabbitMqPort,
    string rabbitMqUserName,
    string rabbitMqPassword,
    SslOption sslOption,
    int channelPoolSize)
    : IRabbitMqConfiguration
{
    public string RabbitMqHost { get; } = rabbitMqHost;
    public string RabbitVirtualHost { get; } = rabbitVirtualHost;
    public int RabbitMqPort { get; } = rabbitMqPort;
    public string RabbitMqUserName { get; } = rabbitMqUserName;
    public string RabbitMqPassword { get; } = rabbitMqPassword;
    public SslOption SslOption { get; } = sslOption;
    public int ChannelPoolSize { get; } = channelPoolSize;
}
