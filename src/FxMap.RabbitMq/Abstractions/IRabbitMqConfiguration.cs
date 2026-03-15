using RabbitMQ.Client;

namespace FxMap.RabbitMq.Abstractions;

internal interface IRabbitMqConfiguration
{
    string RabbitMqHost { get; }
    string RabbitVirtualHost { get; }
    int RabbitMqPort { get; }
    string RabbitMqUserName { get; }
    string RabbitMqPassword { get; }
    SslOption SslOption { get; }
}
