using FxMap.Abstractions;
using FxMap.Abstractions.Transporting;

namespace FxMap.Azure.ServiceBus.Abstractions;

internal interface IAzureServiceBusServer<TModel, TDistributedKey> : IRequestServer<TModel, TDistributedKey>
    where TDistributedKey : IDistributedKey where TModel : class;
