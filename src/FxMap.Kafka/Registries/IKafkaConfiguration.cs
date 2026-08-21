using Confluent.Kafka;

namespace FxMap.Kafka.Registries;

internal interface IKafkaConfiguration
{
    string KafkaHost { get; }
    KafkaSslOptions KafkaSslOptions { get; }
    string GetRequestTopic(Type type);
    void ApplySsl(ClientConfig clientConfig);
}
