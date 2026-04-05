using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using Amazon;
using Amazon.Runtime;
using Amazon.SQS;
using Amazon.SQS.Model;
using FxMap.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FxMap.Models;
using FxMap.Aws.Sqs.Abstractions;
using FxMap.Aws.Sqs.Constants;
using FxMap.Extensions;
using FxMap.Implementations;
using FxMap.Responses;
using FxMap.Telemetry;

namespace FxMap.Aws.Sqs.Implementations;

internal class SqsServer : ISqsServer
{
    private static readonly ConcurrentDictionary<string, Type> DistributedKeyAssemblyCached = [];
    private readonly ILogger<SqsServer> _logger;

    // Backpressure: limit concurrent processing
    private readonly SemaphoreSlim _semaphore;

    private AmazonSQSClient _sqsClient;
    private readonly List<string> _requestQueueUrls = [];
    private readonly CancellationTokenSource _processingCts = new();
    private readonly IServiceProvider _serviceProvider;
    private readonly IMapperConfiguration _mapperConfiguration;
    private readonly ISqsConfiguration _sqsConfiguration;

    public SqsServer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetService<ILogger<SqsServer>>();
        _mapperConfiguration = serviceProvider.GetRequiredService<IMapperConfiguration>();
        _sqsConfiguration = serviceProvider.GetRequiredService<ISqsConfiguration>();
        _semaphore = new SemaphoreSlim(_mapperConfiguration.MaxConcurrentProcessing,
            _mapperConfiguration.MaxConcurrentProcessing);
    }

    private const string TransportName = "sqs";

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        // Configure AWS credentials
        AWSCredentials credentials = null;
        if (!string.IsNullOrEmpty(_sqsConfiguration.AwsAccessKeyId) &&
            !string.IsNullOrEmpty(_sqsConfiguration.AwsSecretAccessKey))
        {
            credentials =
                new BasicAWSCredentials(_sqsConfiguration.AwsAccessKeyId, _sqsConfiguration.AwsSecretAccessKey);
        }

        // Create SQS client
        var config = new AmazonSQSConfig
        {
            RegionEndpoint = _sqsConfiguration.AwsRegion ?? RegionEndpoint.USEast1
        };

        // Support LocalStack for testing
        if (!string.IsNullOrEmpty(_sqsConfiguration.ServiceUrl)) config.ServiceURL = _sqsConfiguration.ServiceUrl;

        _sqsClient = credentials != null
            ? new AmazonSQSClient(credentials, config)
            : new AmazonSQSClient(config);
        var fxMapConfiguration = _serviceProvider.GetRequiredService<IMapperConfiguration>();
        var distributedKeyTypes = fxMapConfiguration.DistributedKeyMapHandlers.Keys.ToList();
        if (distributedKeyTypes is not { Count: > 0 }) return;

        // Create request queues for each distributed key type
        foreach (var queueName in distributedKeyTypes.Select(distributedKeyType =>
                     _sqsConfiguration.GetQueueName(distributedKeyType)))
        {
            try
            {
                // Try to get existing queue
                var getQueueUrlResponse = await _sqsClient.GetQueueUrlAsync(queueName, cancellationToken);
                _requestQueueUrls.Add(getQueueUrlResponse.QueueUrl);
            }
            catch (QueueDoesNotExistException)
            {
                // Create new queue
                var createQueueResponse = await _sqsClient.CreateQueueAsync(new CreateQueueRequest
                {
                    QueueName = queueName,
                    Attributes = new Dictionary<string, string>
                    {
                        {
                            SqsConstants.AttributeVisibilityTimeout,
                            SqsConstants.DefaultVisibilityTimeout.ToString()
                        },
                        {
                            SqsConstants.AttributeReceiveMessageWaitTimeSeconds,
                            SqsConstants.DefaultWaitTimeSeconds.ToString()
                        }
                    }
                }, cancellationToken);

                _requestQueueUrls.Add(createQueueResponse.QueueUrl);
            }
        }

        // Start receiver loops for each queue (parallel processing)
        foreach (var queueUrl in _requestQueueUrls) ProcessQueueAsync(queueUrl, cancellationToken).Forget();
    }

    private async Task ProcessQueueAsync(string queueUrl, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                // Long polling receive
                var response = await _sqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
                {
                    QueueUrl = queueUrl,
                    MaxNumberOfMessages = SqsConstants.MaxNumberOfMessages,
                    WaitTimeSeconds = SqsConstants.DefaultWaitTimeSeconds,
                    MessageAttributeNames = [SqsConstants.MessageAttributeAll]
                }, ct);

                if (response.Messages.Count == 0) continue;

                // Process messages with backpressure control
                var processingTasks = response.Messages.Select(async message =>
                {
                    // Acquire slot inside task (parallel)
                    await _semaphore.WaitAsync(ct);
                    try
                    {
                        // Process message (now awaited, not fire-and-forget)
                        ProcessMessageAsync(queueUrl, message, ct).Forget();
                        return message.ReceiptHandle;
                    }
                    catch (Exception ex)
                    {
                        _logger?.LogError(ex, "Error processing message {CorrelationId}",
                            message.MessageAttributes.GetValueOrDefault(SqsConstants.MessageAttributeCorrelationId)
                                ?.StringValue ?? "Unknown");
                        _semaphore.Release();
                        return message.ReceiptHandle;
                    }
                });

                var results = await Task.WhenAll(processingTasks);

                // Batch delete all processed messages
                var receiptHandles = results.ToList();

                if (receiptHandles.Count > 0)
                    await _sqsClient.DeleteMessageBatchAsync(new DeleteMessageBatchRequest
                    {
                        QueueUrl = queueUrl,
                        Entries = receiptHandles.Select((handle, index) => new DeleteMessageBatchRequestEntry
                        {
                            Id = index.ToString(),
                            ReceiptHandle = handle
                        }).ToList()
                    }, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger?.LogError(ex, "Error processing queue {QueueUrl}", queueUrl);
                await Task.Delay(1000, ct);
            }
        }
    }

    private async Task ProcessMessageAsync(string queueUrl, Message message, CancellationToken stoppingToken)
    {
        // Extract metadata
        var correlationId = message.MessageAttributes
            .GetValueOrDefault(SqsConstants.MessageAttributeCorrelationId)?.StringValue ?? "Unknown";
        var replyToQueueUrl = message.MessageAttributes
            .GetValueOrDefault(SqsConstants.MessageAttributeReplyTo)
            ?.StringValue;
        var distributedKeyTypeString = message.MessageAttributes
            .GetValueOrDefault(SqsConstants.MessageAttributeType)
            ?.StringValue;

        // Extract parent trace context
        ActivityContext parentContext = default;
        if (message.MessageAttributes.TryGetValue(SqsConstants.MessageAttributeTraceparent, out var traceparent))
            ActivityContext.TryParse(traceparent.StringValue, null, out parentContext);

        // Parse message to get distributed key name
        var fxmapRequest = JsonSerializer.Deserialize<DistributedMapRequest>(message.Body);
        var distributedKeyName = distributedKeyTypeString?.Split(',')[0].Split('.').Last() ?? "Unknown";

        // Start server-side activity
        using var activity = FxMapActivitySource.StartServerActivity(distributedKeyName, parentContext);
        var stopwatch = Stopwatch.StartNew();

        // Create timeout CTS
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        cts.CancelAfter(_mapperConfiguration.DefaultRequestTimeout);
        var cancellationToken = cts.Token;

        try
        {
            // Add messaging tags to activity
            activity?.SetMessagingTags(system: TransportName, destination: queueUrl, messageId: correlationId,
                operation: "process");

            // Emit diagnostic event
            FxMapDiagnostics.MessageReceive(TransportName, queueUrl, correlationId);

            var mapperConfiguration = _serviceProvider.GetRequiredService<IMapperConfiguration>();
            var receivedPipelineOrchestrator = DistributedKeyAssemblyCached.GetOrAdd(distributedKeyTypeString,
                distributedKeyAssembly =>
                {
                    var (distributedKeyType, handlerType) =
                        mapperConfiguration.GetDistributedTypeData(distributedKeyAssembly);
                    var modelType = handlerType.GetGenericArguments()[0];
                    return typeof(ReceivedPipelinesOrchestrator<,>).MakeGenericType(modelType, distributedKeyType);
                });

            using var scope = _serviceProvider.CreateScope();
            var server = scope.ServiceProvider
                .GetService(receivedPipelineOrchestrator) as ReceivedPipelinesOrchestrator;
            ArgumentNullException.ThrowIfNull(server);

            var headers = message.MessageAttributes
                .ToDictionary(a => a.Key, b => b.Value.StringValue);
            var data = await server.ExecuteAsync(fxmapRequest, headers, cancellationToken);
            var response = Result.Success(data);

            // Send response back to reply queue
            if (!string.IsNullOrEmpty(replyToQueueUrl))
            {
                await _sqsClient.SendMessageAsync(new SendMessageRequest
                {
                    QueueUrl = replyToQueueUrl,
                    MessageBody = JsonSerializer.Serialize(response),
                    MessageAttributes = new Dictionary<string, MessageAttributeValue>
                    {
                        {
                            SqsConstants.MessageAttributeCorrelationId,
                            new MessageAttributeValue { DataType = "String", StringValue = correlationId }
                        }
                    }
                }, cancellationToken);
            }

            // Record success
            stopwatch.Stop();
            var itemCount = data?.Items?.Length ?? 0;

            FxMapMetrics.RecordRequest(distributedKeyName, TransportName, stopwatch.Elapsed.TotalMilliseconds,
                itemCount);

            activity?.SetFxMapTags(fxmapRequest?.Expressions, fxmapRequest?.SelectorIds, itemCount);

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            _logger?.LogWarning("Request timeout for <{DistributedKey}>", distributedKeyTypeString);
            var response = Result.Failed(new TimeoutException($"Request timeout for {distributedKeyTypeString}"));

            // Record timeout as error
            FxMapMetrics.RecordError(distributedKeyName, TransportName, stopwatch.Elapsed.TotalMilliseconds,
                "TimeoutException");

            activity?.SetStatus(ActivityStatusCode.Error, "Request timeout");

            await SendResponseAsync(replyToQueueUrl, correlationId, response, stoppingToken);
            throw; // Re-throw to mark message for deletion in batch
        }
        catch (Exception e)
        {
            stopwatch.Stop();

            _logger?.LogError(e, "Error while responding <{DistributedKey}>", distributedKeyTypeString);
            var response = Result.Failed(e);

            // Record error
            FxMapMetrics.RecordError(distributedKeyName, TransportName, stopwatch.Elapsed.TotalMilliseconds,
                e.GetType().Name);

            FxMapDiagnostics.RequestError(distributedKeyName, TransportName, e, stopwatch.Elapsed);

            activity?.RecordException(e);
            activity?.SetStatus(ActivityStatusCode.Error, e.Message);

            await SendResponseAsync(replyToQueueUrl, correlationId, response, stoppingToken);
            throw; // Re-throw to mark message for deletion in batch
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private async Task SendResponseAsync(string replyToQueueUrl, string correlationId, Result response,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(replyToQueueUrl)) return;

        try
        {
            await _sqsClient.SendMessageAsync(new SendMessageRequest
            {
                QueueUrl = replyToQueueUrl,
                MessageBody = JsonSerializer.Serialize(response),
                MessageAttributes = new Dictionary<string, MessageAttributeValue>
                {
                    {
                        SqsConstants.MessageAttributeCorrelationId,
                        new MessageAttributeValue { DataType = "String", StringValue = correlationId }
                    }
                }
            }, cancellationToken);
        }
        catch
        {
            // Ignore errors when sending error response
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_processingCts is not null) await _processingCts.CancelAsync();
        _sqsClient?.Dispose();
        _semaphore?.Dispose();
        _processingCts?.Dispose();
    }
}