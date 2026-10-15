using NATS.Client.Core;

namespace FxMap.Nats.Configuration;

internal sealed class NatsConfigurator : INatsConfigurator
{
    internal NatsOpts NatsOption { get; private set; }
    internal string TopicPrefixValue { get; private set; }
    
    public void TopicPrefix(string topicPrefix) => TopicPrefixValue = topicPrefix;

    public void NatsOpts(Action<FxNatsOpts> options)
    {
        var opts = new FxNatsOpts();
        options(opts);
        NatsOption = opts.ToNatsOpts();
    }
}