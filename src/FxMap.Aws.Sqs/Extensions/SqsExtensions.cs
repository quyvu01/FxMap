using Microsoft.Extensions.DependencyInjection;
using FxMap.Abstractions.Transporting;
using FxMap.Aws.Sqs.Abstractions;
using FxMap.Aws.Sqs.Configuration;
using FxMap.Aws.Sqs.BackgroundServices;
using FxMap.Aws.Sqs.Implementations;
using FxMap.Registries;
using FxMap.Configuration;
using FxMap.Supervision;

namespace FxMap.Aws.Sqs.Extensions;

public static class SqsExtensions
{
    public static void AddSqs(this MapConfigurator mapRegister, Action<SqsConfigurator> options)
    {
        var config = new SqsConfigurator();
        options.Invoke(config);
        var services = mapRegister.ServiceCollection;
        services.AddSingleton<ISqsServer, SqsServer>();
        services.AddSingleton<IRequestClient, SqsRequestClient>();

        // Register supervisor options: global > default
        var supervisorOptions = FxMapStatics.SupervisorOptions ?? new SupervisorOptions();
        services.AddSingleton(supervisorOptions);

        // Use SqsSupervisorWorker with supervisor pattern
        services.AddHostedService<SqsSupervisorWorker>();
    }
}
