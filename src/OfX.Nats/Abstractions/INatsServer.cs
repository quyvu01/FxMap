using OfX.Abstractions;
using OfX.Abstractions.Transporting;

namespace OfX.Nats.Abstractions;

internal interface INatsServer<TModel, TDistributedKey> : IRequestServer<TModel, TDistributedKey>
    where TDistributedKey : IDistributedKey where TModel : class;
