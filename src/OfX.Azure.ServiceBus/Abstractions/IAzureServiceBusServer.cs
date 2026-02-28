using OfX.Abstractions;
using OfX.Abstractions.Transporting;

namespace OfX.Azure.ServiceBus.Abstractions;

internal interface IAzureServiceBusServer<TModel, TDistributedKey> : IRequestServer<TModel, TDistributedKey>
    where TDistributedKey : IDistributedKey where TModel : class;
