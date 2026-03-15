using FxMap.Kafka.Registries;

namespace FxMap.Kafka.Registries;

public sealed class KafkaConfigurator
{
    internal string KafkaHostValue { get; private set; }
    internal KafkaSslOptions KafkaSslOptionsValue { get; private set; }

    public void Host(string host) => KafkaHostValue = host;

    public void Ssl(Action<KafkaSslOptions> kafkaSslOptions)
    {
        var options = new KafkaSslOptions();
        kafkaSslOptions(options);
        KafkaSslOptionsValue = options;
    }
}
