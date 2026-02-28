using Microsoft.Extensions.DependencyInjection;
using FxMap.Abstractions.Transporting;
using FxMap.Kafka.Abstractions;
using FxMap.Kafka.BackgroundServices;
using FxMap.Kafka.Implementations;
using FxMap.Registries;
using FxMap.Configuration;
using FxMap.Kafka.Registries;
using FxMap.Supervision;

namespace FxMap.Kafka.Extensions;

public static class KafkaExtensions
{
    public static void AddKafka(this MapConfigurator mapRegister, Action<KafkaConfigurator> options)
    {
        var config = new KafkaConfigurator();
        options.Invoke(config);
        var services = mapRegister.ServiceCollection;
        services.AddSingleton(typeof(IKafkaServer<,>), typeof(KafkaServer<,>));
        services.AddSingleton<IRequestClient, KafkaClient>();

        // Register supervisor options: global > default
        var supervisorOptions = FxMapStatics.SupervisorOptions ?? new SupervisorOptions();
        services.AddSingleton(supervisorOptions);

        // Use KafkaSupervisorWorker with supervisor pattern
        services.AddHostedService<KafkaSupervisorWorker>();
    }
}