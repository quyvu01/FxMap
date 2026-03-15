using Microsoft.Extensions.DependencyInjection;
using FxMap.Abstractions.Transporting;
using FxMap.RabbitMq.Abstractions;
using FxMap.RabbitMq.BackgroundServices;
using FxMap.RabbitMq.Implementations;
using FxMap.Registries;
using FxMap.RabbitMq.Registries;
using FxMap.Supervision;

namespace FxMap.RabbitMq.Extensions;

public static class RabbitMqExtensions
{
    public static void AddRabbitMq(this MapConfigurator mapRegister, Action<RabbitMqConfigurator> options)
    {
        var config = new RabbitMqConfigurator();
        options.Invoke(config);
        var services = mapRegister.Services;
        services.AddSingleton<IRabbitMqConfiguration>(new RabbitMqConfiguration(
            config.HostValue,
            config.VirtualHostValue,
            config.PortValue,
            config.Credential.UserNameValue,
            config.Credential.PasswordValue,
            config.Credential.SslOptionValue));
        services.AddSingleton<IRabbitMqServer, RabbitMqServer>();
        services.AddSingleton<IRequestClient, RabbitMqRequestClient>();

        // Register supervisor options: global > default
        var supervisorOptions = mapRegister.SupervisorOptions ?? new SupervisorOptions();
        services.AddSingleton(supervisorOptions);

        // Use RabbitMqSupervisorWorker with supervisor pattern
        services.AddHostedService<RabbitMqSupervisorWorker>();
    }
}