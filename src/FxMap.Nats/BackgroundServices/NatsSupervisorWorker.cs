using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using FxMap.Abstractions.Transporting;
using FxMap.Nats.Abstractions;
using FxMap.Configuration;
using FxMap.Supervision;

namespace FxMap.Nats.BackgroundServices;

internal sealed class NatsSupervisorWorker(
    IServiceProvider serviceProvider,
    ILogger<NatsSupervisorWorker> logger,
    SupervisorOptions options = null)
    : BackgroundService
{
    private readonly SupervisorOptions _options = options ?? new SupervisorOptions();
    private ServerSupervisor _supervisor;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _supervisor = new ServerSupervisor(_options, serviceProvider.GetService<ILogger<ServerSupervisor>>());

        // Subscribe to health changes for logging
        _supervisor.ServerHealthChanged += OnServerHealthChanged;

        try
        {
            // Register all NATS servers
            foreach (var (distributedKeyType, handlerType) in FxMapStatics.DistributedKeyMapHandlers.Value)
            {
                var modelArg = handlerType.GetGenericArguments()[0];
                var serverType = typeof(INatsServer<,>).MakeGenericType(modelArg, distributedKeyType);
                var server = serviceProvider.GetService(serverType);

                if (server is not IRequestServer requestServer)
                {
                    logger.LogWarning("Failed to resolve NATS server for {DistributedKey}", distributedKeyType.Name);
                    continue;
                }

                var serverId = $"NatsServer<{modelArg.Name},{distributedKeyType.Name}>";
                _supervisor.RegisterServer(serverId, requestServer);
            }

            // Start the supervisor
            await _supervisor.StartAsync(stoppingToken);

            // Wait until cancellation
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Normal shutdown
            logger.LogInformation("NATS Supervisor shutting down...");
        }
        finally
        {
            await _supervisor.StopAsync(CancellationToken.None);
            _supervisor.ServerHealthChanged -= OnServerHealthChanged;
            await _supervisor.DisposeAsync();
        }
    }

    private void OnServerHealthChanged(object sender, ServerHealthChangedEventArgs e)
    {
        var logLevel = e.NewHealth switch
        {
            ServerHealth.Healthy => LogLevel.Information,
            ServerHealth.Degraded => LogLevel.Warning,
            ServerHealth.Unhealthy => LogLevel.Warning,
            ServerHealth.CircuitOpen => LogLevel.Error,
            ServerHealth.Stopped => LogLevel.Critical,
            _ => LogLevel.Information
        };

        logger.Log(logLevel, e.Exception, "NATS Server {ServerId} health: {Previous} -> {New}",
            e.ServerId, e.PreviousHealth, e.NewHealth);
    }
}