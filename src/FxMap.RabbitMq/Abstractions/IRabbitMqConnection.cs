using RabbitMQ.Client;

namespace FxMap.RabbitMq.Abstractions;

/// <summary>
/// Owns the single, lazily-created <see cref="IConnection"/> shared by every RabbitMQ transport
/// component (request client, server) in this process, so they multiplex channels over one
/// connection instead of each dialing the broker separately.
/// </summary>
internal interface IRabbitMqConnection
{
    Task<IChannel> CreateChannelAsync(CreateChannelOptions createChannelOptions = null,
        CancellationToken cancellationToken = default);
}
