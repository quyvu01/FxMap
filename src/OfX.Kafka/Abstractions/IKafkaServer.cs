using OfX.Abstractions;
using OfX.Abstractions.Transporting;

namespace OfX.Kafka.Abstractions;

internal interface IKafkaServer<TModel, TDistributedKey> : IRequestServer<TModel, TDistributedKey>
    where TDistributedKey : IDistributedKey where TModel : class;
