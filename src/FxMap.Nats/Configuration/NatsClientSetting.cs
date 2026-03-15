using NATS.Client.Core;

namespace FxMap.Nats.Configuration;

public sealed class NatsClientSetting
{
    public string NatsUrl { get; private set; }
    public NatsOpts NatsOption { get; private set; }
    public string DefaultNatsUrl { get; } = new NatsOpts().Url;
    internal string TopicPrefixValue { get; private set; }

    public void Url(string url) => NatsUrl = url;

    public void TopicPrefix(string topicPrefix) => TopicPrefixValue = topicPrefix;

    public void NatsOpts(NatsOpts options) => NatsOption = options;
}