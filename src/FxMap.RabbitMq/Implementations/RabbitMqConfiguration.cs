using FxMap.RabbitMq.Abstractions;
using RabbitMQ.Client;

namespace FxMap.RabbitMq.Implementations;

internal sealed class RabbitMqConfiguration(
    string rabbitMqHost,
    string rabbitVirtualHost,
    int rabbitMqPort,
    string rabbitMqUserName,
    string rabbitMqPassword,
    SslOption sslOption)
    : IRabbitMqConfiguration
{
    public string RabbitMqHost { get; } = rabbitMqHost;
    public string RabbitVirtualHost { get; } = rabbitVirtualHost;
    public int RabbitMqPort { get; } = rabbitMqPort;
    public string RabbitMqUserName { get; } = rabbitMqUserName;
    public string RabbitMqPassword { get; } = rabbitMqPassword;
    public SslOption SslOption { get; } = sslOption;
}
