using OfX.Abstractions;
using OfX.Abstractions.Transporting;

namespace OfX.Kafka.Abstractions;

internal interface IKafkaServer<TModel, TAttribute> : IRequestServer<TModel, TAttribute>
    where TAttribute : IDistributedKey where TModel : class;
