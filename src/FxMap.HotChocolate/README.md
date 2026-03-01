# FxMap.HotChocolate

**FxMap.HotChocolate** is an integration package that seamlessly connects **FxMap** with the **HotChocolate** GraphQL library ([Hot Chocolate Docs](https://chillicream.com/docs/hotchocolate/v15)).

With **FxMap.HotChocolate**, you get **high-performance, FluentAPI-based data mapping**, making your **GraphQL queries lightning-fast** across distributed systems.

**Write Less Code. Fetch Data Smarter. Scale Effortlessly.**

[Demo Project!](https://github.com/quyvu01/TestFxMap-Demo)

---

## Why FxMap.HotChocolate?

**Effortless Data Mapping** -- Leverage FxMap's FluentAPI-based Data Mapping to simplify GraphQL queries.
**Seamless Integration** -- Works out-of-the-box with HotChocolate and FxMap.
**Blazing Fast Queries** -- Optimized data retrieval for high-performance systems.
**Scalable & Flexible** -- Works across distributed environments with multiple transport layers.

---

## Installation

To install the FxMap.HotChocolate package, use the following NuGet command:

```bash
dotnet add package FxMap.HotChocolate
```

Or via the NuGet Package Manager:

```bash
Install-Package FxMap.HotChocolate
```

---

## How to Use

### 1. Register FxMap.HotChocolate

Add FxMap.HotChocolate to your service configuration during application startup:

```csharp
var registerBuilder = builder.Services.AddGraphQLServer()
    .AddQueryType<Query>();

builder.Services.AddFxMap(cfg =>
{
    cfg.AddEntitiesFromAssemblyContaining<SomeEntityAssemblyMarker>();
    cfg.AddProfilesFromAssemblyContaining<SomeProfileAssemblyMarker>();
    cfg.AddNats(config => config.Url("nats://localhost:4222"));
})
.AddHotChocolate(cfg => cfg.AddRequestExecutorBuilder(registerBuilder));

...

var app = builder.Build();

app.Run();
```
`Note:` FxMap.HotChocolate will dynamically create the `ObjectTypeExtension<T>` for **ResponseType**. So If you want to create **ObjectType** for some object e.g: `UserResponse`,
please use `ObjectTypeExtension<T>` instead of `ObjectType<T>`.

That All, enjoy your moment!

| Package Name                                                   | Description                                                                                                                | .NET Version | Document                                                                                    |
|----------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------|--------------|---------------------------------------------------------------------------------------------|
| **Core**                                                       |                                                                                                                            |
| [FxMap][FxMap.nuget]                                           | FxMap core                                                                                                                 | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/README.md)                             |
| **Data Providers**                                             |                                                                                                                            |
| [FxMap.EntityFrameworkCore][FxMap.EntityFrameworkCore.nuget]    | FxMap extension package using EntityFramework to fetch data                                                                | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.EntityFrameworkCore/README.md) |
| [FxMap.MongoDb][FxMap.MongoDb.nuget]                           | FxMap extension package using MongoDb to fetch data                                                                        | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.MongoDb/README.md)            |
| **Integrations**                                               |                                                                                                                            |
| [FxMap.HotChocolate][FxMap.HotChocolate.nuget]                 | FxMap.HotChocolate is an integration package with HotChocolate for FxMap.                                                  | 8.0, 9.0     | This Document                                                                               |
| **Transports**                                                 |                                                                                                                            |
| [FxMap.Grpc][FxMap.Grpc.nuget]                                 | FxMap.Grpc is an extension package for FxMap that leverages gRPC for efficient data transportation.                        | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Grpc/README.md)               |
| [FxMap.Kafka][FxMap.Kafka.nuget]                               | FxMap.Kafka is an extension package for FxMap that leverages Kafka for efficient data transportation.                      | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Kafka/README.md)              |
| [FxMap.Nats][FxMap.Nats.nuget]                                 | FxMap.Nats is an extension package for FxMap that leverages Nats for efficient data transportation.                        | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Nats/README.md)               |
| [FxMap.RabbitMq][FxMap.RabbitMq.nuget]                         | FxMap.RabbitMq is an extension package for FxMap that leverages RabbitMq for efficient data transportation.                | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.RabbitMq/README.md)           |

---

[FxMap.nuget]: https://www.nuget.org/packages/FxMap/

[FxMap.EntityFrameworkCore.nuget]: https://www.nuget.org/packages/FxMap.EntityFrameworkCore/

[FxMap.MongoDb.nuget]: https://www.nuget.org/packages/FxMap.MongoDb/

[FxMap.HotChocolate.nuget]: https://www.nuget.org/packages/FxMap.HotChocolate/

[FxMap.Grpc.nuget]: https://www.nuget.org/packages/FxMap.Grpc/

[FxMap.Nats.nuget]: https://www.nuget.org/packages/FxMap.Nats/

[FxMap.RabbitMq.nuget]: https://www.nuget.org/packages/FxMap.RabbitMq/

[FxMap.Kafka.nuget]: https://www.nuget.org/packages/FxMap.Kafka/
