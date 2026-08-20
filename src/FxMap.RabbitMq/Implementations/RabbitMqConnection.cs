using FxMap.RabbitMq.Abstractions;
using FxMap.RabbitMq.Constants;
using FxMap.RabbitMq.Registries;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;

namespace FxMap.RabbitMq.Implementations;

/// <summary>
/// Single shared <see cref="IConnection"/>, lazily connected on first use and reused by every
/// RabbitMQ transport component. Registered as a singleton so <see cref="RabbitMqRequestClient"/>
/// and <see cref="RabbitMqServer"/> multiplex their channels over the same broker connection
/// instead of each opening one.
/// </summary>
internal sealed class RabbitMqConnection(
    IRabbitMqConfiguration rabbitMqConfiguration,
    ILogger<RabbitMqConnection> logger)
    : IRabbitMqConnection, IAsyncDisposable
{
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private IConnection _connection;

    public async Task<IChannel> CreateChannelAsync(CreateChannelOptions createChannelOptions = null,
        CancellationToken cancellationToken = default)
    {
        var connection = await GetConnectionAsync(cancellationToken);
        return await connection.CreateChannelAsync(createChannelOptions, cancellationToken);
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken cancellationToken)
    {
        if (_connection is { IsOpen: true }) return _connection;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_connection is { IsOpen: true }) return _connection;
            _connection = await CreateConnectionAsync(cancellationToken);
            return _connection;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task<IConnection> CreateConnectionAsync(CancellationToken cancellationToken)
    {
        var userName = rabbitMqConfiguration.RabbitMqUserName ?? FxMapRabbitMqConstants.DefaultUserName;
        var password = rabbitMqConfiguration.RabbitMqPassword ?? FxMapRabbitMqConstants.DefaultPassword;
        var connectionFactory = new ConnectionFactory
        {
            HostName = rabbitMqConfiguration.RabbitMqHost,
            VirtualHost = rabbitMqConfiguration.RabbitVirtualHost,
            Port = rabbitMqConfiguration.RabbitMqPort,
            Ssl = rabbitMqConfiguration.SslOption ?? new SslOption(),
            UserName = userName,
            Password = password,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
        };

        var connection = await connectionFactory.CreateConnectionAsync(cancellationToken);

        connection.ConnectionShutdownAsync += (_, args) =>
        {
            logger.LogWarning("RabbitMQ connection shut down (ReplyText={ReplyText}); " +
                               "AutomaticRecoveryEnabled will attempt to reconnect.", args.ReplyText);
            return Task.CompletedTask;
        };
        connection.RecoverySucceededAsync += (_, _) =>
        {
            logger.LogInformation("RabbitMQ connection recovered successfully.");
            return Task.CompletedTask;
        };
        connection.ConnectionRecoveryErrorAsync += (_, args) =>
        {
            logger.LogWarning(args.Exception, "RabbitMQ connection recovery attempt failed; will retry.");
            return Task.CompletedTask;
        };

        return connection;
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null) await _connection.CloseAsync();
        _initLock.Dispose();
    }
}
