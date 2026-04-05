using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FxMap.Abstractions;
using FxMap.Models;
using FxMap.Implementations;
using FxMap.Kafka.Abstractions;
using FxMap.Kafka.Constants;
using FxMap.Kafka.Wrappers;
using FxMap.Responses;
using FxMap.Telemetry;

namespace FxMap.Kafka.Implementations;

internal class KafkaServer<TModel, TDistributedKey> : IKafkaServer<TModel, TDistributedKey>, IDisposable
    where TDistributedKey : IDistributedKey
    where TModel : class
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IConsumer<string, string> _consumer;
    private readonly IProducer<string, string> _producer;
    private readonly string _requestTopic;
    private readonly ILogger<KafkaServer<TModel, TDistributedKey>> _logger;
    private readonly IMapperConfiguration _mapperConfiguration;
    private readonly string _kafkaBootstrapServers;
    private const string TransportName = "kafka";

    // Backpressure: limit concurrent processing (configurable via FxMapConfigurator.SetMaxConcurrentProcessing)
    private readonly SemaphoreSlim _semaphore;

    private bool _topicsCreated;

    public KafkaServer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _mapperConfiguration = serviceProvider.GetRequiredService<IMapperConfiguration>();
        var kafkaConfiguration = serviceProvider.GetRequiredService<IKafkaConfiguration>();
        _semaphore = new SemaphoreSlim(_mapperConfiguration.MaxConcurrentProcessing,
            _mapperConfiguration.MaxConcurrentProcessing);

        _kafkaBootstrapServers = kafkaConfiguration.KafkaHost;
        var consumerConfig = new ConsumerConfig
        {
            GroupId = KafkaConstants.ServerGroupId,
            BootstrapServers = _kafkaBootstrapServers,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false
        };

        var producerConfig = new ProducerConfig { BootstrapServers = _kafkaBootstrapServers };

        if (kafkaConfiguration.KafkaSslOptions != null)
        {
            kafkaConfiguration.ApplySsl(producerConfig);
            kafkaConfiguration.ApplySsl(consumerConfig);
        }

        _consumer = new ConsumerBuilder<string, string>(consumerConfig)
            .SetKeyDeserializer(Deserializers.Utf8)
            .SetValueDeserializer(Deserializers.Utf8)
            .Build();

        _producer = new ProducerBuilder<string, string>(producerConfig)
            .SetKeySerializer(Serializers.Utf8)
            .SetValueSerializer(Serializers.Utf8)
            .Build();
        _requestTopic = kafkaConfiguration.GetRequestTopic(typeof(TDistributedKey));
        _logger = serviceProvider.GetService<ILogger<KafkaServer<TModel, TDistributedKey>>>();
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        // Lazy topic creation
        if (!_topicsCreated)
        {
            await CreateTopicsAsync();
            _topicsCreated = true;
        }

        await Task.Yield();
        _consumer.Subscribe(_requestTopic);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var consumeResult = _consumer.Consume(cancellationToken);
                if (consumeResult?.Message == null) continue;

                // Backpressure - wait for available slot
                await _semaphore.WaitAsync(cancellationToken);
                try
                {
                    _ = ProcessMessageWithReleaseAsync(consumeResult, cancellationToken);
                }
                catch
                {
                    // If firing the task fails, release semaphore to prevent leak
                    _semaphore.Release();
                    throw;
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Normal shutdown
                break;
            }
            catch (ConsumeException ex)
            {
                _logger?.LogError(ex, "Error consuming Kafka message for <{DistributedKey}>", typeof(TDistributedKey).Name);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error processing Kafka message for <{DistributedKey}>", typeof(TDistributedKey).Name);
            }
        }
    }

    private async Task ProcessMessageWithReleaseAsync(ConsumeResult<string, string> consumeResult,
        CancellationToken stoppingToken)
    {
        try
        {
            await ProcessMessageAsync(consumeResult, stoppingToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task ProcessMessageAsync(ConsumeResult<string, string> consumeResult, CancellationToken stoppingToken)
    {
        var messageUnWrapped = JsonSerializer
            .Deserialize<KafkaMessageWrapped<DistributedMapRequest>>(consumeResult.Message.Value);

        // Extract parent trace context
        ActivityContext parentContext = default;
        var traceparentHeader = consumeResult.Message.Headers?.FirstOrDefault(h => h.Key == "traceparent");
        if (traceparentHeader != null)
        {
            var traceparent = Encoding.UTF8.GetString(traceparentHeader.GetValueBytes());
            ActivityContext.TryParse(traceparent, null, out parentContext);
        }

        var distributedKeyName = typeof(TDistributedKey).Name;
        using var activity = FxMapActivitySource.StartServerActivity(distributedKeyName, parentContext);
        var stopwatch = Stopwatch.StartNew();

        // Create timeout CTS
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        cts.CancelAfter(_mapperConfiguration.DefaultRequestTimeout);
        var cancellationToken = cts.Token;

        // Properly dispose the service scope
        using var serviceScope = _serviceProvider.CreateScope();

        try
        {
            activity?.SetMessagingTags(
                system: TransportName,
                destination: _requestTopic,
                messageId: consumeResult.Message.Key,
                operation: "process");

            FxMapDiagnostics.MessageReceive(TransportName, _requestTopic, consumeResult.Message.Key);

            var pipeline = serviceScope.ServiceProvider
                .GetRequiredService<ReceivedPipelinesOrchestrator<TModel, TDistributedKey>>();

            var message = messageUnWrapped.Message;
            var query = new MapRequest<TDistributedKey>(message.SelectorIds, message.Expressions);
            var headers = consumeResult.Message.Headers?
                .ToDictionary(a => a.Key, h => Encoding.UTF8.GetString(h.GetValueBytes())) ?? [];

            var requestContext = new RequestContextImpl<TDistributedKey>(query, headers, cancellationToken);
            var data = await pipeline.ExecuteAsync(requestContext);

            var response = Result.Success(data);
            await SendResponseAsync(consumeResult, messageUnWrapped.ReplyTo, response, cancellationToken);

            // Record success metrics
            stopwatch.Stop();
            var itemCount = data?.Items?.Length ?? 0;

            FxMapMetrics.RecordRequest(
                distributedKeyName,
                TransportName,
                stopwatch.Elapsed.TotalMilliseconds,
                itemCount);

            activity?.SetFxMapTags(message.Expressions, message.SelectorIds, itemCount);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            _logger?.LogWarning("Request timeout for <{DistributedKey}>", distributedKeyName);

            FxMapMetrics.RecordError(
                distributedKeyName,
                TransportName,
                stopwatch.Elapsed.TotalMilliseconds,
                "TimeoutException");

            activity?.SetStatus(ActivityStatusCode.Error, "Request timeout");

            var response = Result
                .Failed(new TimeoutException($"Request timeout for {distributedKeyName}"));
            await TrySendResponseAsync(consumeResult, messageUnWrapped.ReplyTo, response, stoppingToken);
        }
        catch (Exception e)
        {
            stopwatch.Stop();

            _logger?.LogError(e, "Error while responding <{DistributedKey}>", distributedKeyName);

            FxMapMetrics.RecordError(
                distributedKeyName,
                TransportName,
                stopwatch.Elapsed.TotalMilliseconds,
                e.GetType().Name);

            FxMapDiagnostics.RequestError(distributedKeyName, TransportName, e, stopwatch.Elapsed);

            activity?.RecordException(e);
            activity?.SetStatus(ActivityStatusCode.Error, e.Message);

            var response = Result.Failed(e);
            await TrySendResponseAsync(consumeResult, messageUnWrapped.ReplyTo, response, stoppingToken);
        }
        finally
        {
            // Always commit to avoid reprocessing - message has been handled (success or error)
            TryCommit(consumeResult);
        }
    }

    private async Task SendResponseAsync(ConsumeResult<string, string> consumeResult, string replyTo,
        Result response, CancellationToken cancellationToken)
    {
        await _producer.ProduceAsync(replyTo, new Message<string, string>
        {
            Key = consumeResult.Message.Key,
            Value = JsonSerializer.Serialize(response)
        }, cancellationToken);
    }

    private async Task TrySendResponseAsync(ConsumeResult<string, string> consumeResult, string replyTo,
        Result response, CancellationToken cancellationToken)
    {
        try
        {
            await SendResponseAsync(consumeResult, replyTo, response, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send response for <{DistributedKey}>", typeof(TDistributedKey).Name);
        }
    }

    private void TryCommit(ConsumeResult<string, string> consumeResult)
    {
        try
        {
            _consumer.Commit(consumeResult);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to commit offset for <{DistributedKey}>", typeof(TDistributedKey).Name);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        Dispose();
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _consumer?.Dispose();
        _producer?.Dispose();
        _semaphore.Dispose();
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
                Name = _requestTopic,
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
            _logger?.LogWarning(ex, "Failed to create Kafka topic {Topic}", _requestTopic);
        }
    }
}