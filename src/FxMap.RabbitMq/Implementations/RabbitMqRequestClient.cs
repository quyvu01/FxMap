using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FxMap.Abstractions;
using FxMap.Abstractions.Transporting;
using FxMap.Exceptions;
using FxMap.Extensions;
using FxMap.RabbitMq.Abstractions;
using FxMap.RabbitMq.Constants;
using FxMap.RabbitMq.Extensions;
using FxMap.Responses;
using FxMap.Telemetry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FxMap.RabbitMq.Implementations;

internal class RabbitMqRequestClient(
    IMapperConfiguration mapperConfiguration,
    IRabbitMqConnection rabbitMqConnection,
    RabbitMqChannelPool publishChannelPool)
    : IRequestClient, IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, TaskCompletionSource<BasicDeliverEventArgs>> _eventArgsMapper = new();
    private readonly SemaphoreSlim _initLock = new(1, 1);

    // Dedicated to consuming replies off the exclusive reply queue - there is only ever one
    // consumer for it, so pooling this one wouldn't help.
    private IChannel _replyChannel;
    private AsyncEventingBasicConsumer _consumer;
    private string _replyQueueName;
    private bool _initialized;
    private const string RoutingKey = FxMapRabbitMqConstants.RoutingKey;
    private const string TransportName = "rabbitmq";

    public async Task<ItemsResponse<DataResponse>> RequestAsync<TDistributedKey>(
        RequestContext<TDistributedKey> requestContext) where TDistributedKey : IDistributedKey
    {
        // Start client-side activity for distributed tracing
        using var activity = FxMapActivitySource.StartClientActivity<TDistributedKey>(TransportName);

        try
        {
            // Lazy initialization - thread-safe
            await EnsureInitializedAsync(requestContext.CancellationToken);

            var exchangeName = typeof(TDistributedKey).GetExchangeName();
            var cancellationToken = requestContext.CancellationToken;
            var correlationId = Guid.NewGuid().ToString();
            var props = new BasicProperties
            {
                CorrelationId = correlationId,
                ReplyTo = _replyQueueName,
                Type = typeof(TDistributedKey).AssemblyQualifiedName
            };
            props.Headers ??= new Dictionary<string, object>();
            requestContext.Headers?.ForEach(h => props.Headers.Add(h.Key, h.Value));

            // Propagate W3C trace context
            if (activity != null)
            {
                if (!string.IsNullOrEmpty(activity.Id))
                    props.Headers.TryAdd("traceparent", Encoding.UTF8.GetBytes(activity.Id));
                if (!string.IsNullOrEmpty(activity.TraceStateString))
                    props.Headers.TryAdd("tracestate", Encoding.UTF8.GetBytes(activity.TraceStateString));

                activity.SetMessagingTags(system: TransportName, destination: exchangeName, messageId: correlationId,
                    operation: "publish");

                activity.SetFxMapTags(requestContext.Query.Expressions, requestContext.Query.SelectorIds);
            }

            var tcs = new TaskCompletionSource<BasicDeliverEventArgs>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _eventArgsMapper.TryAdd(correlationId, tcs);

            try
            {
                var messageSerialize = JsonSerializer.Serialize(requestContext.Query);
                var messageBytes = Encoding.UTF8.GetBytes(messageSerialize);

                var channel = await publishChannelPool.RentAsync(cancellationToken);
                try
                {
                    await channel.BasicPublishAsync(exchangeName, routingKey: RoutingKey,
                        mandatory: true, basicProperties: props, body: messageBytes,
                        cancellationToken: cancellationToken);
                }
                finally
                {
                    await publishChannelPool.ReturnAsync(channel, cancellationToken);
                }

                // Wait with timeout
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                cts.CancelAfter(mapperConfiguration.DefaultRequestTimeout);

                await using var _ = cts.Token.Register(() => tcs.TrySetCanceled());

                var eventArgs = await tcs.Task;
                var resultAsString = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
                var response = JsonSerializer.Deserialize<Result>(resultAsString);

                if (response is null)
                    throw new DistributedMapException.ReceivedException("Received null response from server");

                if (!response.IsSuccess)
                    throw response.Fault?.ToException()
                          ?? new DistributedMapException.ReceivedException("Unknown error from server");

                var itemCount = response.Data?.Items?.Length ?? 0;
                activity?.SetFxMapTags(itemCount: itemCount);
                activity?.SetStatus(ActivityStatusCode.Ok);

                return response.Data;
            }
            finally
            {
                // Cleanup on any exit path
                _eventArgsMapper.TryRemove(correlationId, out _);
            }
        }
        catch (Exception ex)
        {
            activity?.RecordException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            throw;
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;
            await InitializeAsync(cancellationToken);
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private async Task InitializeAsync(CancellationToken cancellationToken)
    {
        _replyChannel = await rabbitMqConnection.CreateChannelAsync(cancellationToken: cancellationToken);
        var queueDeclareResult = await _replyChannel.QueueDeclareAsync(cancellationToken: cancellationToken);
        _replyQueueName = queueDeclareResult.QueueName;
        _consumer = new AsyncEventingBasicConsumer(_replyChannel);
        _consumer.ReceivedAsync += async (sender, ea) =>
        {
            var correlationId = ea.BasicProperties.CorrelationId;
            if (string.IsNullOrEmpty(correlationId) || !_eventArgsMapper.TryRemove(correlationId, out var tcs)) return;
            tcs.TrySetResult(ea);
            var ackChannel = ((AsyncEventingBasicConsumer)sender).Channel;
            await ackChannel.BasicAckAsync(ea.DeliveryTag, false, ea.CancellationToken);
        };
        await _replyChannel.BasicConsumeAsync(_replyQueueName, false, _consumer, cancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (_replyChannel is not null) await _replyChannel.CloseAsync();
        _initLock.Dispose();
    }
}