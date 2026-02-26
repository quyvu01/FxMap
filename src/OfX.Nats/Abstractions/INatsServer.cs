using OfX.Abstractions;
using OfX.Abstractions.Transporting;

namespace OfX.Nats.Abstractions;

internal interface INatsServer<TModel, TAttribute> : IRequestServer<TModel, TAttribute>
    where TAttribute : IDistributedKey where TModel : class;
