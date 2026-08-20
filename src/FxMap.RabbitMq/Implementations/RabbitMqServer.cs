using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FxMap.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FxMap.Models;
using FxMap.Extensions;
using FxMap.Implementations;
using FxMap.RabbitMq.Abstractions;
using FxMap.RabbitMq.Constants;
using FxMap.RabbitMq.Extensions;
using FxMap.RabbitMq.Registries;
using FxMap.Responses;
using FxMap.Telemetry;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace FxMap.RabbitMq.Implementations;

internal class RabbitMqServer : IRabbitMqServer
{
    private static readonly ConcurrentDictionary<string, Type> DistributedKeyAssemblyCached = new();
    private readonly ILogger<RabbitMqServer> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IMapperConfiguration _mapperConfiguration;
    private readonly IRabbitMqConfiguration _rabbitMqConfiguration;
    private readonly IRabbitMqConnection _rabbitMqConnection;
    private readonly ushort _concurrentMessageLimit;
    private readonly List<IChannel> _consumerChannels = [];
    private const string TransportName = "rabbitmq";

    public RabbitMqServer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetService<ILogger<RabbitMqServer>>();
        _mapperConfiguration = serviceProvider.GetRequiredService<IMapperConfiguration>();
        _rabbitMqConfiguration = serviceProvider.GetRequiredService<IRabbitMqConfiguration>();
        _rabbitMqConnection = serviceProvider.GetRequiredService<IRabbitMqConnection>();
        _concurrentMessageLimit = (ushort)_mapperConfiguration.MaxConcurrentProcessing;
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var queueName = $"{FxMapRabbitMqConstants.QueueNamePrefix}-{AppDomain.CurrentDomain.FriendlyName.ToLower()}";
        const string routingKey = FxMapRabbitMqConstants.RoutingKey;

        var distributedKeyTypes = _mapperConfiguration.DistributedKeyMapHandlers.Keys.ToList();
        if (distributedKeyTypes is not { Count: > 0 }) return;

        // Topology declare uses its own short-lived channel over the shared connection - it must
        // not be tied to any of the long-lived consumer channels created below.
        await using (var declareChannel = await _rabbitMqConnection
                         .CreateChannelAsync(cancellationToken: cancellationToken))
        {
            await declareChannel.QueueDeclareAsync(queue: queueName, durable: false, exclusive: false,
                autoDelete: false, arguments: null, cancellationToken: cancellationToken);

            var exchangeNames = distributedKeyTypes.Select(distributedKeyType =>
                distributedKeyType.GetExchangeName());

            foreach (var exchangeName in exchangeNames)
            {
                await declareChannel.ExchangeDeclareAsync(exchangeName, type: ExchangeType.Direct,
                    cancellationToken: cancellationToken);
                await declareChannel.QueueBindAsync(queue: queueName, exchangeName, routingKey,
                    cancellationToken: cancellationToken);
            }
        }

        var createChannelOptions = new CreateChannelOptions(
            publisherConfirmationsEnabled: false,
            publisherConfirmationTrackingEnabled: false,
            consumerDispatchConcurrency: _concurrentMessageLimit);

        for (var i = 0; i < _rabbitMqConfiguration.ChannelPoolSize; i++)
        {
            var channel = await _rabbitMqConnection.CreateChannelAsync(createChannelOptions, cancellationToken);
            _consumerChannels.Add(channel);

            var consumer = new AsyncEventingBasicConsumer(channel);
            consumer.ReceivedAsync += (sender, ea) => ProcessMessageAsync(sender, ea, cancellationToken);

            await channel.BasicConsumeAsync(queueName, false, consumer, cancellationToken: cancellationToken);
        }
    }

    private async Task ProcessMessageAsync(object sender, BasicDeliverEventArgs ea, CancellationToken stoppingToken)
    {
        var cons = (AsyncEventingBasicConsumer)sender;
        var ch = cons.Channel;
        var body = ea.Body.ToArray();
        var props = ea.BasicProperties;
        var replyProps = new BasicProperties { CorrelationId = props.CorrelationId };

        // Extract parent trace context from headers
        ActivityContext parentContext = default;
        if (props.Headers?.TryGetValue("traceparent", out var traceparent) ?? false)
            ActivityContext.TryParse(Encoding.UTF8.GetString((byte[])traceparent!), null, out parentContext);

        // Parse message to get attribute name
        var message = JsonSerializer.Deserialize<DistributedMapRequest>(Encoding.UTF8.GetString(body));
        var distributedKeyName = props.Type?.Split(',')[0].Split('.').Last() ?? "Unknown";

        // Start server-side activity
        using var activity = FxMapActivitySource.StartServerActivity(distributedKeyName, parentContext);

        // Create timeout CTS
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        cts.CancelAfter(_mapperConfiguration.DefaultRequestTimeout);
        var cancellationToken = cts.Token;

        try
        {
            // Add messaging tags to activity
            activity?.SetMessagingTags(system: TransportName, destination: ea.Exchange, messageId: props.CorrelationId,
                operation: "process");

            var receivedPipelineOrchestrator = DistributedKeyAssemblyCached.GetOrAdd(props.Type,
                distributedKeyAssembly =>
                {
                    var (distributedKeyType, handlerType) = _mapperConfiguration
                        .GetDistributedTypeData(distributedKeyAssembly);
                    var modelType = handlerType.GetGenericArguments()[0];
                    return typeof(ReceivedPipelinesOrchestrator<,>).MakeGenericType(modelType, distributedKeyType);
                });

            using var scope = _serviceProvider.CreateScope();
            var server = scope.ServiceProvider
                .GetService(receivedPipelineOrchestrator) as ReceivedPipelinesOrchestrator;
            ArgumentNullException.ThrowIfNull(server);

            var headers = props.Headers?
                .ToDictionary(a => a.Key, b => b.Value.ToString()) ?? [];
            var data = await server.ExecuteAsync(message, headers, cancellationToken);
            var response = Result.Success(data);
            var responseAsString = JsonSerializer.Serialize(response);
            var responseBytes = Encoding.UTF8.GetBytes(responseAsString);
            await ch.BasicPublishAsync(exchange: string.Empty, routingKey: props.ReplyTo!,
                mandatory: true, basicProperties: replyProps, body: responseBytes,
                cancellationToken: cancellationToken);

            var itemCount = data?.Items?.Length ?? 0;

            activity?.SetFxMapTags(message?.Expressions, message?.SelectorIds, itemCount);

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger?.LogWarning("Request timeout for <{DistributedKey}>", props.Type);
            var response = Result.Failed(new TimeoutException($"Request timeout for {props.Type}"));
            activity?.SetStatus(ActivityStatusCode.Error, "Request timeout");
            await SendResponseAsync(ch, props.ReplyTo, replyProps, response, cancellationToken);
        }
        catch (Exception e)
        {
            _logger?.LogError(e, "Error while responding <{DistributedKey}>", props.Type);
            var response = Result.Failed(e);
            activity?.RecordException(e);
            activity?.SetStatus(ActivityStatusCode.Error, e.Message);

            await SendResponseAsync(ch, props.ReplyTo, replyProps, response, cancellationToken);
        }
        finally
        {
            try
            {
                await ch.BasicAckAsync(deliveryTag: ea.DeliveryTag, multiple: false,
                    cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to acknowledge message");
            }
        }
    }

    private static async Task SendResponseAsync(IChannel ch, string replyTo, BasicProperties replyProps,
        Result response, CancellationToken cancellationToken)
    {
        try
        {
            var responseAsString = JsonSerializer.Serialize(response);
            var responseBytes = Encoding.UTF8.GetBytes(responseAsString);
            await ch.BasicPublishAsync(exchange: string.Empty, routingKey: replyTo!,
                mandatory: true, basicProperties: replyProps, body: responseBytes,
                cancellationToken: cancellationToken);
        }
        catch
        {
            // Ignore errors when sending error response
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        foreach (var channel in _consumerChannels)
        {
            try
            {
                await channel.CloseAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Failed to close RabbitMQ consumer channel");
            }
        }

        _consumerChannels.Clear();
    }
}