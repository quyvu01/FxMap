using FxMap.Abstractions;
using FxMap.Abstractions.Transporting;

namespace FxMap.Nats.Abstractions;

internal interface INatsServer<TModel, TDistributedKey> : IRequestServer<TModel, TDistributedKey>
    where TDistributedKey : IDistributedKey where TModel : class;
