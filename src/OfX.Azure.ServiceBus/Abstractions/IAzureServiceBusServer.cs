using OfX.Abstractions;
using OfX.Abstractions.Transporting;

namespace OfX.Azure.ServiceBus.Abstractions;

internal interface IAzureServiceBusServer<TModel, TAttribute> : IRequestServer<TModel, TAttribute>
    where TAttribute : IDistributedKey where TModel : class;
