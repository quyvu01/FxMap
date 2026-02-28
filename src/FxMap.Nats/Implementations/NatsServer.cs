using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NATS.Client.Core;
using FxMap.Abstractions;
using FxMap.Models;
using FxMap.Implementations;
using FxMap.Nats.Abstractions;
using FxMap.Nats.Extensions;
using FxMap.Nats.Wrappers;
using FxMap.Responses;
using FxMap.Configuration;
using FxMap.Telemetry;

namespace FxMap.Nats.Implementations;

internal sealed class NatsServer<TModel, TDistributedKey>(IServiceProvider serviceProvider)
    : INatsServer<TModel, TDistributedKey>
    where TDistributedKey : IDistributedKey where TModel : class
{
    private const string TransportName = "nats";

    private readonly ILogger<NatsServer<TModel, TDistributedKey>> _logger =
        serviceProvider.GetService<ILogger<NatsServer<TModel, TDistributedKey>>>();

    private readonly NatsClientWrapper _natsClientWrapped = serviceProvider
        .GetRequiredService<NatsClientWrapper>();

    // Backpressure: limit concurrent processing (configurable via FxMapConfigurator.SetMaxConcurrentProcessing)
    private readonly SemaphoreSlim _semaphore = new(FxMapStatics.MaxConcurrentProcessing,
        FxMapStatics.MaxConcurrentProcessing);

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var natsScribeAsync = _natsClientWrapped.NatsClient
            .SubscribeAsync<FxMapRequest>(typeof(TDistributedKey).GetNatsSubject(), cancellationToken: cancellationToken);

        await foreach (var message in natsScribeAsync)
        {
            // Wait for available slot (backpressure)
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                _ = ProcessMessageWithReleaseAsync(message, cancellationToken);
            }
            catch
            {
                // If firing the task fails, release semaphore to prevent leak
                _semaphore.Release();
                throw;
            }
        }
    }

    private async Task ProcessMessageWithReleaseAsync(NatsMsg<FxMapRequest> message, CancellationToken stoppingToken)
    {
        try
        {
            await ProcessMessageAsync(message, stoppingToken);
        }
        finally

        {
            _semaphore.Release();
        }
    }

    private async Task ProcessMessageAsync(NatsMsg<FxMapRequest> message, CancellationToken stoppingToken)
    {
        if (message.Data is null) return;

        // Extract parent trace context from headers
        ActivityContext parentContext = default;
        if (message.Headers?.TryGetValue("traceparent", out var traceparent) ?? false)
            ActivityContext.TryParse(traceparent.ToString(), null, out parentContext);

        // Start server-side activity
        using var activity = FxMapActivitySource.StartServerActivity(typeof(TDistributedKey).Name, parentContext);

        var stopwatch = Stopwatch.StartNew();

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        cts.CancelAfter(FxMapStatics.DefaultRequestTimeout);
        var cancellationToken = cts.Token;
        try
        {
            // Add messaging tags to activity
            activity?.SetMessagingTags(
                system: TransportName,
                destination: typeof(TDistributedKey).GetNatsSubject(),
                operation: "process");

            // Emit diagnostic event
            FxMapDiagnostics.MessageReceive(
                TransportName,
                typeof(TDistributedKey).GetNatsSubject(),
                message.Subject);

            using var serviceScope = serviceProvider.CreateScope();
            var pipeline = serviceScope.ServiceProvider
                .GetRequiredService<ReceivedPipelinesOrchestrator<TModel, TDistributedKey>>();
            var headers = message.Headers?
                .ToDictionary(a => a.Key, b => b.Value.ToString()) ?? [];
            var requestOf = new MapRequest<TDistributedKey>(message.Data.SelectorIds, message.Data.Expressions);
            var requestContext = new RequestContextImpl<TDistributedKey>(requestOf, headers, cancellationToken);

            var data = await pipeline.ExecuteAsync(requestContext);
            var response = Result.Success(data);

            await message.ReplyAsync(response, cancellationToken: cancellationToken);

            // Record success
            stopwatch.Stop();
            var itemCount = data?.Items?.Length ?? 0;

            FxMapMetrics.RecordRequest(typeof(TDistributedKey).Name, TransportName, stopwatch.Elapsed.TotalMilliseconds,
                itemCount);

            activity?.SetFxMapTags(message.Data.Expressions, message.Data.SelectorIds, itemCount);

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            _logger?.LogWarning("Request timeout for <{Attribute}>", typeof(TDistributedKey).Name);
            var response = Result.Failed(new TimeoutException($"Request timeout for {typeof(TDistributedKey).Name}"));

            // Record timeout as error
            FxMapMetrics.RecordError(
                typeof(TDistributedKey).Name,
                TransportName,
                stopwatch.Elapsed.TotalMilliseconds,
                "TimeoutException");

            activity?.SetStatus(ActivityStatusCode.Error, "Request timeout");

            await TrySendErrorResponseAsync(message, response);
        }
        catch (Exception e)
        {
            stopwatch.Stop();

            _logger?.LogError(e, "Error while responding <{Attribute}>", typeof(TDistributedKey).Name);
            var response = Result.Failed(e);

            // Record error
            FxMapMetrics.RecordError(
                typeof(TDistributedKey).Name,
                TransportName,
                stopwatch.Elapsed.TotalMilliseconds,
                e.GetType().Name);

            FxMapDiagnostics.RequestError(
                typeof(TDistributedKey).Name,
                TransportName,
                e,
                stopwatch.Elapsed);

            activity?.RecordException(e);
            activity?.SetStatus(ActivityStatusCode.Error, e.Message);

            await TrySendErrorResponseAsync(message, response);
        }
    }

    private async Task TrySendErrorResponseAsync(NatsMsg<FxMapRequest> message, Result response)
    {
        try
        {
            if (message.ReplyTo is not null)
            {
                await _natsClientWrapped.NatsClient.PublishAsync(message.ReplyTo, response);
            }
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Failed to send error response for <{Attribute}>", typeof(TDistributedKey).Name);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        _semaphore.Dispose();
        return Task.CompletedTask;
    }
}