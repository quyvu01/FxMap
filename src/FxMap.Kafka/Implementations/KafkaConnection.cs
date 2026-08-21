using Confluent.Kafka;
using FxMap.Kafka.Abstractions;
using FxMap.Kafka.Registries;

namespace FxMap.Kafka.Implementations;

/// <summary>
/// Single shared <see cref="IProducer{TKey,TValue}"/>, built once and reused by every Kafka
/// transport component. Registered as a singleton so <see cref="KafkaClient"/> and
/// <see cref="KafkaServer{TModel,TDistributedKey}"/> share one producer instead of each dialing
/// their own.
/// </summary>
internal sealed class KafkaConnection : IKafkaConnection, IDisposable
{
    private readonly IKafkaConfiguration _kafkaConfiguration;

    public IProducer<string, string> Producer { get; }

    public KafkaConnection(IKafkaConfiguration kafkaConfiguration)
    {
        _kafkaConfiguration = kafkaConfiguration;

        var producerConfig = new ProducerConfig { BootstrapServers = kafkaConfiguration.KafkaHost };
        if (kafkaConfiguration.KafkaSslOptions != null)
            kafkaConfiguration.ApplySsl(producerConfig);

        Producer = new ProducerBuilder<string, string>(producerConfig)
            .SetKeySerializer(Serializers.Utf8)
            .SetValueSerializer(Serializers.Utf8)
            .Build();
    }

    public IConsumer<string, string> CreateConsumer(string groupId, AutoOffsetReset autoOffsetReset,
        bool enableAutoCommit)
    {
        var consumerConfig = new ConsumerConfig
        {
            GroupId = groupId,
            BootstrapServers = _kafkaConfiguration.KafkaHost,
            AutoOffsetReset = autoOffsetReset,
            EnableAutoCommit = enableAutoCommit
        };
        if (_kafkaConfiguration.KafkaSslOptions != null)
            _kafkaConfiguration.ApplySsl(consumerConfig);

        return new ConsumerBuilder<string, string>(consumerConfig)
            .SetKeyDeserializer(Deserializers.Utf8)
            .SetValueDeserializer(Deserializers.Utf8)
            .Build();
    }

    public void Dispose() => Producer.Dispose();
}
