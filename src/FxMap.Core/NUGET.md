# FxMap.Core

The engine of [FxMap](https://fxmapper.net) without a dependency injection container: profiles (`ProfileOf<T>`), the
expression language, the distributed mapper and `FxMapBuilder`. It does not reference
Microsoft.Extensions.DependencyInjection.

```csharp
var mapper = FxMapBuilder.Create()
    .AddProfilesFromAssemblyContaining<OrderResponseProfile>()
    .UseRequestClient(requestClient)   // e.g. FxMapNats.CreateRequestClient(natsClient)
    .SetRetryPolicy(3)
    .Build();

await mapper.MapDataAsync(orders);
```

With a container, use the `FxMap` package and `services.AddFxMap(...)`: it references this package.

[Documentation](https://fxmapper.net)
