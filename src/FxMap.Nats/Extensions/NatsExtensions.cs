using Microsoft.Extensions.DependencyInjection;
using NATS.Net;
using FxMap.Abstractions.Transporting;
using FxMap.Nats.Abstractions;
using FxMap.Nats.Configuration;
using FxMap.Nats.BackgroundServices;
using FxMap.Nats.Implementations;
using FxMap.Nats.Wrappers;
using FxMap.Registries;
using FxMap.Supervision;

namespace FxMap.Nats.Extensions;

public static class NatsExtensions
{
    public static void AddNats(this MapConfigurator mapRegister, Action<NatsClientSetting> options)
    {
        var natsSetting = new NatsClientSetting();
        options.Invoke(natsSetting);
        var services = mapRegister.Services;
        var defaultNatsUrl = natsSetting.DefaultNatsUrl;
        services.AddSingleton<INatsConfiguration>(new NatsConfiguration(natsSetting.TopicPrefixValue));
        services.AddSingleton(_ =>
        {
            if (natsSetting.NatsOption is not { } natsOption)
                return new NatsClientWrapper(new NatsClient(natsSetting.NatsUrl ?? defaultNatsUrl));
            var natsUrl = natsOption.Url != natsSetting.DefaultNatsUrl ? natsOption.Url :
                natsSetting.NatsUrl != defaultNatsUrl ? natsSetting.NatsUrl : defaultNatsUrl;
            return new NatsClientWrapper(new NatsClient(natsOption with { Url = natsUrl }));
        });
        services.AddSingleton(typeof(INatsServer<,>), typeof(NatsServer<,>));

        // Register supervisor options: global > default
        var supervisorOptions = mapRegister.SupervisorOptions ?? new SupervisorOptions();
        services.AddSingleton(supervisorOptions);

        // Use NatsSupervisorWorker with supervisor pattern
        services.AddHostedService<NatsSupervisorWorker>();
        services.AddTransient<IRequestClient, NatsRequestClient>();
    }
}