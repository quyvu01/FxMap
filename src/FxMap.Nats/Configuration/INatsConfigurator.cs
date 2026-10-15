using NATS.Client.Core;

namespace FxMap.Nats.Configuration;

public interface INatsConfigurator
{
    void TopicPrefix(string topicPrefix);

    void NatsOpts(Action<FxNatsOpts> options);
}
