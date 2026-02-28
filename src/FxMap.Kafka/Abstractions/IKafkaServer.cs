using FxMap.Abstractions;
using FxMap.Abstractions.Transporting;

namespace FxMap.Kafka.Abstractions;

internal interface IKafkaServer<TModel, TDistributedKey> : IRequestServer<TModel, TDistributedKey>
    where TDistributedKey : IDistributedKey where TModel : class;
