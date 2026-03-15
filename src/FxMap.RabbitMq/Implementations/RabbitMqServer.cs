using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using System.Text.Json;
using FxMap.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using FxMap.Models;
using FxMap.Exceptions;
using FxMap.Implementations;
using FxMap.RabbitMq.Abstractions;
using FxMap.RabbitMq.Constants;
using FxMap.RabbitMq.Extensions;
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

    // Backpressure: limit concurrent processing (configurable via FxMapConfigurator.SetMaxConcurrentProcessing)
    private readonly SemaphoreSlim _semaphore;

    private IConnection _connection;
    private IChannel _channel;
    private const string TransportName = "rabbitmq";

    public RabbitMqServer(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
        _logger = serviceProvider.GetService<ILogger<RabbitMqServer>>();
        _mapperConfiguration = serviceProvider.GetRequiredService<IMapperConfiguration>();
        _rabbitMqConfiguration = serviceProvider.GetRequiredService<IRabbitMqConfiguration>();
        _semaphore = new SemaphoreSlim(_mapperConfiguration.MaxConcurrentProcessing,
            _mapperConfiguration.MaxConcurrentProcessing);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        var queueName = $"{FxMapRabbitMqConstants.QueueNamePrefix}-{AppDomain.CurrentDomain.FriendlyName.ToLower()}";
        const string routingKey = FxMapRabbitMqConstants.RoutingKey;

        var userName = _rabbitMqConfiguration.RabbitMqUserName ?? FxMapRabbitMqConstants.DefaultUserName;
        var password = _rabbitMqConfiguration.RabbitMqPassword ?? FxMapRabbitMqConstants.DefaultPassword;
        var connectionFactory = new ConnectionFactory
        {
            HostName = _rabbitMqConfiguration.RabbitMqHost,
            VirtualHost = _rabbitMqConfiguration.RabbitVirtualHost,
            Port = _rabbitMqConfiguration.RabbitMqPort,
            Ssl = _rabbitMqConfiguration.SslOption ?? new SslOption(),
            UserName = userName,
            Password = password,
            // Enable automatic recovery
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
        };

        _connection = await connectionFactory.CreateConnectionAsync(cancellationToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await _channel.QueueDeclareAsync(queue: queueName, durable: false, exclusive: false,
            autoDelete: false, arguments: null, cancellationToken: cancellationToken);

        var distributedKeyTypes = _mapperConfiguration.DistributedKeyMapHandlers.Keys.ToList();
        if (distributedKeyTypes is not { Count: > 0 }) return;

        foreach (var exchangeName in distributedKeyTypes.Select(distributedKeyType => distributedKeyType.GetExchangeName()))
        {
            await _channel.ExchangeDeclareAsync(exchangeName, type: ExchangeType.Direct,
                cancellationToken: cancellationToken);
            await _channel.QueueBindAsync(queue: queueName, exchangeName, routingKey,
                cancellationToken: cancellationToken);
        }

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (sender, ea) =>
        {
            // Backpressure - wait for available slot
            await _semaphore.WaitAsync(cancellationToken);
            try
            {
                await ProcessMessageAsync(sender, ea, cancellationToken);
            }
            finally
            {
                _semaphore.Release();
            }
        };

        await _channel.BasicConsumeAsync(queueName, false, consumer, cancellationToken: cancellationToken);
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
        var stopwatch = Stopwatch.StartNew();

        // Create timeout CTS
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        cts.CancelAfter(_mapperConfiguration.DefaultRequestTimeout);
        var cancellationToken = cts.Token;

        try
        {
            // Add messaging tags to activity
            activity?.SetMessagingTags(system: TransportName, destination: ea.Exchange, messageId: props.CorrelationId,
                operation: "process");

            // Emit diagnostic event
            FxMapDiagnostics.MessageReceive(TransportName, ea.Exchange, props.CorrelationId);

            var receivedPipelineOrchestrator = DistributedKeyAssemblyCached.GetOrAdd(props.Type, distributedKeyAssembly =>
            {
                var distributedKeyType = Type.GetType(distributedKeyAssembly)!;
                if (!_mapperConfiguration.DistributedKeyMapHandlers.TryGetValue(distributedKeyType, out var handlerType))
                    throw new DistributedMapException.CannotFindHandlerForDistributedKey(distributedKeyType);
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

            // Record success
            stopwatch.Stop();
            var itemCount = data?.Items?.Length ?? 0;

            FxMapMetrics.RecordRequest(distributedKeyName, TransportName, stopwatch.Elapsed.TotalMilliseconds, itemCount);

            activity?.SetFxMapTags(message?.Expressions, message?.SelectorIds, itemCount);

            activity?.SetStatus(ActivityStatusCode.Ok);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();

            _logger?.LogWarning("Request timeout for <{DistributedKey}>", props.Type);
            var response = Result.Failed(new TimeoutException($"Request timeout for {props.Type}"));

            // Record timeout as error
            FxMapMetrics.RecordError(distributedKeyName, TransportName, stopwatch.Elapsed.TotalMilliseconds,
                "TimeoutException");

            activity?.SetStatus(ActivityStatusCode.Error, "Request timeout");

            await SendResponseAsync(ch, props.ReplyTo, replyProps, response, cancellationToken);
        }
        catch (Exception e)
        {
            stopwatch.Stop();

            _logger?.LogError(e, "Error while responding <{DistributedKey}>", props.Type);
            var response = Result.Failed(e);

            // Record error
            FxMapMetrics.RecordError(distributedKeyName, TransportName, stopwatch.Elapsed.TotalMilliseconds, e.GetType().Name);

            FxMapDiagnostics.RequestError(distributedKeyName, TransportName, e, stopwatch.Elapsed);

            activity?.RecordException(e);
            activity?.SetStatus(ActivityStatusCode.Error, e.Message);

            await SendResponseAsync(ch, props.ReplyTo, replyProps, response, stoppingToken);
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
        if (_channel is not null) await _channel.CloseAsync(cancellationToken);
        if (_connection is not null) await _connection.CloseAsync(cancellationToken);
        _semaphore?.Dispose();
    }
}