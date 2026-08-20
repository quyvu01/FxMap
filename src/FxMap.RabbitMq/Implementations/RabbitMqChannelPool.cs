using System.Threading.Channels;
using FxMap.RabbitMq.Abstractions;
using FxMap.RabbitMq.Registries;
using RabbitMQ.Client;

namespace FxMap.RabbitMq.Implementations;

/// <summary>
/// Singleton, fixed-size pool of <see cref="IChannel"/> instances rented from the shared
/// <see cref="IRabbitMqConnection"/>. Callers rent a channel, use it, then return it; a channel
/// that was closed by the broker while checked out is transparently replaced on return instead of
/// being handed back to the pool broken. The underlying bounded channel is created lazily, on the
/// first rent, so construction stays synchronous for DI.
/// </summary>
internal sealed class RabbitMqChannelPool(
    IRabbitMqConnection rabbitMqConnection,
    IRabbitMqConfiguration rabbitMqConfiguration)
    : IAsyncDisposable
{
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private Channel<IChannel> _pool;

    public async Task<IChannel> RentAsync(CancellationToken cancellationToken = default)
    {
        await EnsureInitializedAsync(cancellationToken);
        return await _pool.Reader.ReadAsync(cancellationToken);
    }

    public async Task ReturnAsync(IChannel channel, CancellationToken cancellationToken = default)
    {
        if (!channel.IsOpen)
            channel = await rabbitMqConnection.CreateChannelAsync(cancellationToken: cancellationToken);
        await _pool.Writer.WriteAsync(channel, cancellationToken);
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_pool is not null) return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_pool is not null) return;

            var size = rabbitMqConfiguration.ChannelPoolSize;
            var pool = Channel.CreateBounded<IChannel>(new BoundedChannelOptions(size)
            {
                FullMode = BoundedChannelFullMode.Wait
            });
            for (var i = 0; i < size; i++)
            {
                var channel = await rabbitMqConnection.CreateChannelAsync(cancellationToken: cancellationToken);
                await pool.Writer.WriteAsync(channel, cancellationToken);
            }

            _pool = pool;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_pool is null)
        {
            _initLock.Dispose();
            return;
        }

        _pool.Writer.TryComplete();
        while (_pool.Reader.TryRead(out var channel))
            await channel.CloseAsync();
        _initLock.Dispose();
    }
}
