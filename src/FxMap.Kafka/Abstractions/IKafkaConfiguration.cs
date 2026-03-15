using Confluent.Kafka;
using FxMap.Kafka.Registries;

namespace FxMap.Kafka.Abstractions;

internal interface IKafkaConfiguration
{
    string KafkaHost { get; }
    KafkaSslOptions KafkaSslOptions { get; }
    string GetRequestTopic(Type type);
    void ApplySsl(ClientConfig clientConfig);
}
