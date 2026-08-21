using Confluent.Kafka;

namespace FxMap.Kafka.Abstractions;

/// <summary>
/// Owns the single <see cref="IProducer{TKey,TValue}"/> shared by every Kafka transport component
/// (request client, server) in this process — Confluent's producer is thread-safe and meant to be
/// reused, so there is no benefit to each component building its own. Consumers cannot be shared
/// the same way (each subscribes to a distinct topic under a distinct consumer group), so
/// <see cref="CreateConsumer"/> centralizes the shared bootstrap/SSL config instead.
/// </summary>
internal interface IKafkaConnection
{
    IProducer<string, string> Producer { get; }

    IConsumer<string, string> CreateConsumer(string groupId, AutoOffsetReset autoOffsetReset,
        bool enableAutoCommit);
}
