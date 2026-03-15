using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.DependencyInjection;
using FxMap.Abstractions.Transporting;
using FxMap.Azure.ServiceBus.Abstractions;
using FxMap.Azure.ServiceBus.Configuration;
using FxMap.Azure.ServiceBus.BackgroundServices;
using FxMap.Azure.ServiceBus.Implementations;
using FxMap.Azure.ServiceBus.Wrappers;
using FxMap.Registries;
using FxMap.Supervision;

namespace FxMap.Azure.ServiceBus.Extensions;

public static class AzureServiceBusExtensions
{
    public static void AddAzureServiceBus(this MapConfigurator mapRegister, Action<AzureServiceBusClientSetting> options)
    {
        var setting = new AzureServiceBusClientSetting();
        options.Invoke(setting);
        var connectionString = setting.ConnectionString;
        var serviceBusClientOptions = setting.ServiceBusClientOptions;
        var services = mapRegister.Services;
        services.AddSingleton<IAzureServiceBusConfiguration>(
            new AzureServiceBusConfiguration(setting.TopicPrefixValue, setting.MaxConcurrentSessionsValue));
        services.AddSingleton(_ =>
        {
            var client = new ServiceBusClient(connectionString, serviceBusClientOptions);
            var adminClient = new ServiceBusAdministrationClient(connectionString);
            var clientWrapper = new AzureServiceBusClientWrapper(client, adminClient);
            return clientWrapper;
        });

        services.AddSingleton(typeof(IAzureServiceBusServer<,>), typeof(AzureServiceBusServer<,>));
        services.AddSingleton(typeof(OpenAzureServiceBusClient<>));
        services.AddSingleton<IRequestClient, AzureServiceBusClient>();

        // Register supervisor options: global > default
        var supervisorOptions = mapRegister.SupervisorOptions ?? new SupervisorOptions();
        services.AddSingleton(supervisorOptions);

        // Use AzureServiceBusSupervisorWorker with supervisor pattern
        services.AddHostedService<AzureServiceBusSupervisorWorker>();
    }
}