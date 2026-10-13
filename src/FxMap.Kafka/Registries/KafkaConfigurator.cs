namespace FxMap.Kafka.Registries;

public sealed class KafkaConfigurator
{
    internal string KafkaHostValue { get; private set; }
    internal KafkaSslOptions KafkaSslOptionsValue { get; private set; }

    internal int PartitionsValue { get; private set; } = 1;
    internal short ReplicationFactorValue { get; private set; } = 1;

    public void Host(string host) => KafkaHostValue = host;

    /// <summary>
    /// The number of partitions of the request topic of each distributed key, when FxMap creates it. Default: 1.
    /// </summary>
    /// <remarks>
    /// The requests are spread over the partitions (the key of a message is its correlation id), and the instances of a service
    /// share them as one consumer group: more partitions allow more instances to work at the same time (an instance gets whole
    /// partitions, so use at least as many partitions as instances). A topic that already exists is not changed: increase its
    /// partitions with the tools of Kafka. The reply topic of a client is read by one consumer and always has one partition.
    /// </remarks>
    public void Partitions(int partitions)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(partitions, 1);
        PartitionsValue = partitions;
    }

    /// <summary>
    /// The replication factor of the topics that FxMap creates (request and reply topics). Default: 1. A cluster of several brokers
    /// needs a higher value (usually 3) for the topics to survive the loss of a broker; it cannot be higher than the number of brokers.
    /// </summary>
    public void ReplicationFactor(short replicationFactor)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(replicationFactor, (short)1);
        ReplicationFactorValue = replicationFactor;
    }

    public void Ssl(Action<KafkaSslOptions> kafkaSslOptions)
    {
        var options = new KafkaSslOptions();
        kafkaSslOptions(options);
        KafkaSslOptionsValue = options;
    }
}
