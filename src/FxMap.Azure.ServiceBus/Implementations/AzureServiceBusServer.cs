using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FxMap.Abstractions;
using FxMap.Models;
using FxMap.Azure.ServiceBus.Abstractions;
using FxMap.Azure.ServiceBus.Extensions;
using FxMap.Azure.ServiceBus.Statics;
using FxMap.Azure.ServiceBus.Wrappers;
using FxMap.Implementations;
using FxMap.Responses;
using FxMap.Configuration;
using FxMap.Telemetry;

namespace FxMap.Azure.ServiceBus.Implementations;

internal class AzureServiceBusServer<TModel, TDistributedKey>(
    AzureServiceBusClientWrapper clientWrapper,
    IServiceProvider serviceProvider)
    : IAzureServiceBusServer<TModel, TDistributedKey>
    where TDistributedKey : IDistributedKey where TModel : class
{
    private readonly ILogger<AzureServiceBusServer<TModel, TDistributedKey>> _logger =
        serviceProvider.GetService<ILogger<AzureServiceBusServer<TModel, TDistributedKey>>>();

    private const string TransportName = "azureservicebus";

    // Cache senders to avoid creating new ones for each message
    private readonly ConcurrentDictionary<string, ServiceBusSender> _senders = new();
    private ServiceBusSessionProcessor _processor;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var requestQueue = typeof(TDistributedKey).GetAzureServiceBusRequestQueue();
        var options = new ServiceBusSessionProcessorOptions
        {
            MaxConcurrentSessions = AzureServiceBusStatic.MaxConcurrentSessions,
            MaxConcurrentCallsPerSession = 1,
            AutoCompleteMessages = false
        };
        _processor = clientWrapper.ServiceBusClient.CreateSessionProcessor(requestQueue, options);

        _processor.ProcessMessageAsync += args => ProcessMessageAsync(args, cancellationToken);
        _processor.ProcessErrorAsync += ProcessErrorAsync;

        await _processor.StartProcessingAsync(cancellationToken);

        // Wait until cancellation is requested
        try
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown
        }
        finally
        {
            await StopAsync(CancellationToken.None);
        }
    }

    private Task ProcessErrorAsync(ProcessErrorEventArgs args)
    {
        _logger?.LogError(args.Exception, "Azure Service Bus error for <{DistributedKey}>: {ErrorSource}",
            typeof(TDistributedKey).Name, args.ErrorSource);
        return Task.CompletedTask;
    }

    private async Task ProcessMessageAsync(ProcessSessionMessageEventArgs args, CancellationToken stoppingToken)
    {
        var request = args.Message;

        // Extract parent trace context
        ActivityContext parentContext = default;
        if (request.ApplicationProperties?.TryGetValue("traceparent", out var traceparent) ?? false)
            ActivityContext.TryParse(Encoding.UTF8.GetString((byte[])traceparent), null, out parentContext);

        var distributedKeyName = typeof(TDistributedKey).Name;
        var requestQueue = typeof(TDistributedKey).GetAzureServiceBusRequestQueue();
        using var activity = FxMapActivitySource.StartServerActivity(distributedKeyName, parentContext);
        var stopwatch = Stopwatch.StartNew();

        // Create timeout CTS
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        cts.CancelAfter(FxMapStatics.DefaultRequestTimeout);
        var cancellationToken = cts.Token;

        ServiceBusSender sender = null;

        try
        {
            activity?.SetMessagingTags(system: TransportName, destination: requestQueue,
                messageId: request.CorrelationId, operation: "process");

            FxMapDiagnostics.MessageReceive(TransportName, requestQueue, request.CorrelationId);

            var requestDeserialize = JsonSerializer.Deserialize<FxMapRequest>(request.Body);

            using var serviceScope = serviceProvider.CreateScope();
            var pipeline = serviceScope.ServiceProvider
                .GetRequiredService<ReceivedPipelinesOrchestrator<TModel, TDistributedKey>>();

            var headers = request.ApplicationProperties?
                .ToDictionary(a => a.Key, b => b.Value.ToString()) ?? [];
            var requestOf = new MapRequest<TDistributedKey>(requestDeserialize.SelectorIds, requestDeserialize.Expressions);
            var requestContext = new RequestContextImpl<TDistributedKey>(requestOf, headers, cancellationToken);
            var data = await pipeline.ExecuteAsync(requestContext);
            var response = Result.Success(data);

            // Get or create sender (cached)
            sender = _senders.GetOrAdd(request.ReplyTo,
                replyTo => clientWrapper.ServiceBusClient.CreateSender(replyTo));

            await SendResponseAsync(request, sender, response, cancellationToken);
            await args.CompleteMessageAsync(request, cancellationToken);

            // Record success metrics
            stopwatch.Stop();
            var itemCount = data?.Items?.Length ?? 0;

            FxMapMetrics.RecordRequest(distributedKeyName, TransportName,
                stopwatch.Elapsed.TotalMilliseconds, itemCount);

            activity?.SetFxMapTags(requestDeserialize.Expressions, requestDeserialize.SelectorIds, itemCount);
            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            _logger?.LogWarning("Request timeout for <{DistributedKey}>", distributedKeyName);

            FxMapMetrics.RecordError(distributedKeyName, TransportName, stopwatch.Elapsed.TotalMilliseconds,
                "TimeoutException");
            activity?.SetStatus(ActivityStatusCode.Error, "Request timeout");

            var response = Result.Failed(new TimeoutException($"Request timeout for {distributedKeyName}"));
            await TrySendResponseAsync(request, sender, response, stoppingToken);
            await TryCompleteMessageAsync(args, request, stoppingToken);
        }
        catch (Exception e)
        {
            stopwatch.Stop();

            _logger?.LogError(e, "Error while responding <{DistributedKey}>", distributedKeyName);

            FxMapMetrics.RecordError(distributedKeyName, TransportName, stopwatch.Elapsed.TotalMilliseconds, e.GetType().Name);

            FxMapDiagnostics.RequestError(distributedKeyName, TransportName, e, stopwatch.Elapsed);

            activity?.RecordException(e);
            activity?.SetStatus(ActivityStatusCode.Error, e.Message);

            var response = Result.Failed(e);
            await TrySendResponseAsync(request, sender, response, stoppingToken);
            await TryCompleteMessageAsync(args, request, stoppingToken);
        }
    }

    private static async Task SendResponseAsync(ServiceBusReceivedMessage request, ServiceBusSender sender,
        Result response, CancellationToken cancellationToken)
    {
        var responseMessage = new ServiceBusMessage(JsonSerializer.Serialize(response))
        {
            CorrelationId = request.CorrelationId,
            SessionId = request.SessionId
        };
        await sender.SendMessageAsync(responseMessage, cancellationToken);
    }

    private async Task TrySendResponseAsync(ServiceBusReceivedMessage request, ServiceBusSender sender,
        Result response, CancellationToken cancellationToken)
    {
        try
        {
            if (request.ReplyTo == null) return;
            sender ??= _senders.GetOrAdd(request.ReplyTo,
                replyTo => clientWrapper.ServiceBusClient.CreateSender(replyTo));

            await SendResponseAsync(request, sender, response, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send response for <{DistributedKey}>", typeof(TDistributedKey).Name);
        }
    }

    private async Task TryCompleteMessageAsync(ProcessSessionMessageEventArgs args,
        ServiceBusReceivedMessage request, CancellationToken cancellationToken)
    {
        try
        {
            await args.CompleteMessageAsync(request, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to complete message for <{DistributedKey}>", typeof(TDistributedKey).Name);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (_processor != null)
        {
            try
            {
                await _processor.StopProcessingAsync(cancellationToken);
                await _processor.DisposeAsync();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Error stopping Azure Service Bus processor");
            }
        }

        // Dispose all cached senders
        foreach (var sender in _senders.Values)
        {
            try
            {
                await sender.DisposeAsync();
            }
            catch
            {
                // Ignore
            }
        }

        _senders.Clear();
    }
}