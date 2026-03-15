using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.Logging;
using FxMap.Abstractions;
using FxMap.Abstractions.Transporting;
using FxMap.Models;
using FxMap.Exceptions;
using FxMap.Kafka.Abstractions;
using FxMap.Kafka.Constants;
using FxMap.Kafka.Wrappers;
using FxMap.Responses;
using FxMap.Telemetry;

namespace FxMap.Kafka.Implementations;

internal class KafkaClient : IRequestClient, IAsyncDisposable
{
    private readonly IProducer<string, string> _producer;
    private readonly IConsumer<string, string> _consumer;
    private readonly ILogger<KafkaClient> _logger;
    private readonly IMapperConfiguration _mapperConfiguration;
    private readonly IKafkaConfiguration _kafkaConfiguration;
    private readonly string _replyTo;
    private readonly CancellationTokenSource _consumerCts = new();
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;
    private Task _consumerTask;
    private const string TransportName = "kafka";

    private readonly ConcurrentDictionary<string, TaskCompletionSource<Result>>
        _pendingRequests = new();

    private readonly string _kafkaBootstrapServers;

    public KafkaClient(ILogger<KafkaClient> logger, IMapperConfiguration mapperConfiguration,
        IKafkaConfiguration kafkaConfiguration)
    {
        _logger = logger;
        _mapperConfiguration = mapperConfiguration;
        _kafkaConfiguration = kafkaConfiguration;
        _kafkaBootstrapServers = kafkaConfiguration.KafkaHost;
        var producerConfig = new ProducerConfig { BootstrapServers = _kafkaBootstrapServers };
        var consumerConfig = new ConsumerConfig
        {
            GroupId = $"{KafkaConstants.ClientGroupId}-{Guid.NewGuid():N}",
            BootstrapServers = _kafkaBootstrapServers,
            AutoOffsetReset = AutoOffsetReset.Latest,
            EnableAutoCommit = true
        };

        if (kafkaConfiguration.KafkaSslOptions != null)
        {
            kafkaConfiguration.ApplySsl(producerConfig);
            kafkaConfiguration.ApplySsl(consumerConfig);
        }

        _producer = new ProducerBuilder<string, string>(producerConfig)
            .SetKeySerializer(Serializers.Utf8)
            .SetValueSerializer(Serializers.Utf8)
            .Build();
        _consumer = new ConsumerBuilder<string, string>(consumerConfig)
            .SetKeyDeserializer(Deserializers.Utf8)
            .SetValueDeserializer(Deserializers.Utf8)
            .Build();
        _replyTo =
            $"{KafkaConstants.ResponseTopicPrefix}-{AppDomain.CurrentDomain.FriendlyName.ToLower()}-{Guid.NewGuid():N}";
    }

    public async Task<ItemsResponse<DataResponse>> RequestAsync<TDistributedKey>(
        RequestContext<TDistributedKey> requestContext) where TDistributedKey : IDistributedKey
    {
        // Start client-side activity for distributed tracing
        using var activity = FxMapActivitySource.StartClientActivity<TDistributedKey>(TransportName);
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Lazy initialization
            await EnsureInitializedAsync(requestContext.CancellationToken);

            var correlationId = Guid.NewGuid().ToString();
            var topic = _kafkaConfiguration.GetRequestTopic(typeof(TDistributedKey));

            // Propagate W3C trace context
            var message = new KafkaMessageWrapped<DistributedMapRequest>
            {
                Message = new DistributedMapRequest(requestContext.Query.SelectorIds, requestContext.Query.Expressions),
                ReplyTo = _replyTo
            };

            var headers = new Headers();
            if (activity != null)
            {
                if (!string.IsNullOrEmpty(activity.Id))
                    headers.Add("traceparent", System.Text.Encoding.UTF8.GetBytes(activity.Id));
                if (!string.IsNullOrEmpty(activity.TraceStateString))
                    headers.Add("tracestate", System.Text.Encoding.UTF8.GetBytes(activity.TraceStateString));

                activity.SetMessagingTags(system: TransportName, destination: topic, messageId: correlationId,
                    operation: "publish");

                activity.SetFxMapTags(requestContext.Query.Expressions, requestContext.Query.SelectorIds);
            }

            // Emit diagnostic event
            FxMapDiagnostics.RequestStart(typeof(TDistributedKey).Name, TransportName, requestContext.Query.SelectorIds,
                requestContext.Query.Expressions);

            // Track active requests
            FxMapMetrics.UpdateActiveRequests(1);

            var tcs = new TaskCompletionSource<Result>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingRequests.TryAdd(correlationId, tcs);

            try
            {
                // Produce the request
                await _producer.ProduceAsync(topic, new Message<string, string>
                {
                    Key = correlationId,
                    Value = JsonSerializer.Serialize(message),
                    Headers = headers
                }, requestContext.CancellationToken);

                // Wait for response with timeout
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(requestContext.CancellationToken);
                cts.CancelAfter(_mapperConfiguration.DefaultRequestTimeout);

                try
                {
                    var response = await tcs.Task.WaitAsync(cts.Token);

                    if (response is null)
                        throw new DistributedMapException.ReceivedException("Received null response from server");

                    if (!response.IsSuccess)
                        throw response.Fault?.ToException()
                              ?? new DistributedMapException.ReceivedException("Unknown error from server");

                    // Record success metrics
                    stopwatch.Stop();
                    var itemCount = response.Data?.Items?.Length ?? 0;

                    FxMapMetrics.RecordRequest(typeof(TDistributedKey).Name, TransportName,
                        stopwatch.Elapsed.TotalMilliseconds, itemCount);

                    FxMapDiagnostics.RequestStop(typeof(TDistributedKey).Name, TransportName, itemCount, stopwatch.Elapsed);

                    activity?.SetFxMapTags(itemCount: itemCount);
                    activity?.SetStatus(ActivityStatusCode.Ok);

                    return response.Data;
                }
                catch (OperationCanceledException) when (cts.IsCancellationRequested &&
                                                         !requestContext.CancellationToken.IsCancellationRequested)
                {
                    throw new TimeoutException(
                        $"Timeout waiting for Kafka response for {typeof(TDistributedKey).Name}");
                }
            }
            finally
            {
                _pendingRequests.TryRemove(correlationId, out _);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            // Record error metrics
            FxMapMetrics.RecordError(typeof(TDistributedKey).Name, TransportName, stopwatch.Elapsed.TotalMilliseconds,
                ex.GetType().Name);

            FxMapDiagnostics.RequestError(typeof(TDistributedKey).Name, TransportName, ex, stopwatch.Elapsed);

            activity?.RecordException(ex);
            activity?.SetStatus(ActivityStatusCode.Error, ex.Message);

            throw;
        }
        finally
        {
            FxMapMetrics.UpdateActiveRequests(-1);
        }
    }

    private async Task EnsureInitializedAsync(CancellationToken cancellationToken)
    {
        if (_initialized) return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (_initialized) return;
            await CreateTopicsAsync();
            _consumerTask = Task.Run(() => StartConsume(_consumerCts.Token), _consumerCts.Token);
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    private void StartConsume(CancellationToken cancellationToken)
    {
        _consumer.Subscribe(_replyTo);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var consumeResult = _consumer.Consume(cancellationToken);
                if (consumeResult?.Message == null) continue;

                if (!_pendingRequests.TryRemove(consumeResult.Message.Key, out var tcs)) continue;

                var response = JsonSerializer.Deserialize<Result>(consumeResult.Message.Value);
                tcs.TrySetResult(response);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
            catch (ConsumeException ex)
            {
                _logger?.LogError(ex, "Kafka consume error");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing Kafka response");
            }
        }
    }

    private async Task CreateTopicsAsync()
    {
        const int numPartitions = 1;
        const short replicationFactor = 1;

        var config = new AdminClientConfig { BootstrapServers = _kafkaBootstrapServers };

        using var adminClient = new AdminClientBuilder(config).Build();

        try
        {
            var topicSpecification = new TopicSpecification
            {
                Name = _replyTo,
                NumPartitions = numPartitions,
                ReplicationFactor = replicationFactor
            };

            await adminClient.CreateTopicsAsync([topicSpecification]);
        }
        catch (CreateTopicsException ex) when (ex.Results.Any(r => r.Error.Code == ErrorCode.TopicAlreadyExists))
        {
            // Topic already exists - this is fine
        }
        catch (CreateTopicsException ex)
        {
            _logger?.LogWarning(ex, "Failed to create Kafka topic {Topic}", _replyTo);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_consumerTask != null)
        {
            try
            {
                await _consumerTask.WaitAsync(TimeSpan.FromSeconds(10));
            }
            catch (OperationCanceledException)
            {
                // Expected during shutdown
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error waiting for consumer task shutdown");
            }
        }

        await CastAndDispose(_producer);
        await CastAndDispose(_consumer);
        await CastAndDispose(_consumerCts);
        await CastAndDispose(_initLock);
        await CastAndDispose(_consumerTask);
        // Cancel all pending requests
        foreach (var kvp in _pendingRequests) kvp.Value.TrySetCanceled();

        _pendingRequests.Clear();
        return;

        static async ValueTask CastAndDispose(IDisposable resource)
        {
            switch (resource)
            {
                case null:
                    return;
                case IAsyncDisposable resourceAsyncDisposable:
                    await resourceAsyncDisposable.DisposeAsync();
                    break;
                default:
                    resource.Dispose();
                    break;
            }
        }
    }
}