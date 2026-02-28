using NATS.Client.Core;
using FxMap.Nats.Statics;

namespace FxMap.Nats.Configuration;

public sealed class NatsClientSetting
{
    public string NatsUrl { get; private set; }
    public NatsOpts NatsOption { get; private set; }
    public string DefaultNatsUrl { get; } = new NatsOpts().Url;

    public void Url(string url) => NatsUrl = url;

    public void TopicPrefix(string topicPrefix) => NatsStatics.TopicPrefix = topicPrefix;

    public void NatsOpts(NatsOpts options) => NatsOption = options;
}