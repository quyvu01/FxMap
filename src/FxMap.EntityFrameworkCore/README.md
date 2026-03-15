# FxMap.EntityFrameworkCore

FxMap.EntityFrameworkCore is an extension package for FxMap that integrates with Entity Framework Core to simplify data fetching by
leveraging FluentAPI-based data mapping. This extension streamlines data retrieval using EF Core, reducing boilerplate
code and improving maintainability.

[Demo Project!](https://github.com/quyvu01/TestFxMap-Demo)

---

## Introduction

FxMap.EntityFrameworkCore extends the core FxMap library by providing seamless integration with Entity Framework Core. This enables
developers to automatically map and retrieve data directly from a database, leveraging the power of Entity Framework
Core along with FluentAPI-based data mapping.

For example, suppose you have a `UserId` property in your model, and you want to fetch the corresponding `Name`
and `Email` fields from the database. By using FxMap.EntityFrameworkCore, you can configure your entity with `AbstractFxMapConfig<T>`
and define profiles with `ProfileOf<T>`, and the library will handle data fetching for you.

---

## Installation

To install the FxMap.EntityFrameworkCore package, use the following NuGet command:

```bash
dotnet add package FxMap.EntityFrameworkCore
```

Or via the NuGet Package Manager:

```bash
Install-Package FxMap.EntityFrameworkCore
```

---

## How to Use

### 1. Register FxMap.EntityFrameworkCore

Add FxMap.EntityFrameworkCore to your service configuration during application startup:

```csharp
builder.Services.AddFxMap(cfg =>
{
    cfg.AddEntitiesFromAssemblyContaining<SomeEntityAssemblyMarker>();
    cfg.AddProfilesFromAssemblyContaining<SomeProfileAssemblyMarker>();
})
.AddEntityFrameworkCore(options =>
{
    options.AddDbContexts(typeof(TestDbContext), typeof(OtherDbContext));
});
```

### Function Descriptions

#### AddDbContexts

Here, you can use the method `AddDbContexts()`, which takes `DbContext(s)` to executing.

That all, Enjoy your moment!

| Package Name                                                 | Description                                                                                                 | .NET Version   | Document                                                                              |
|--------------------------------------------------------------|-------------------------------------------------------------------------------------------------------------|----------------|---------------------------------------------------------------------------------------|
| **Core**                                                     |                                                                                                             |
| [FxMap][FxMap.nuget]                                         | FxMap core                                                                                                  | 8.0, 9.0, 10.0 | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/README.md)                        |
| **Data Providers**                                           |                                                                                                             |
| [FxMap.EntityFrameworkCore][FxMap.EntityFrameworkCore.nuget] | FxMap extension package using EntityFramework to fetch data                                                 | 8.0, 9.0, 10.0 | This Document                                                                         |
| [FxMap.MongoDb][FxMap.MongoDb.nuget]                         | FxMap extension package using MongoDb to fetch data                                                         | 8.0, 9.0, 10.0 | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.MongoDb/README.md)      |
| **Integrations**                                             |                                                                                                             |
| [FxMap.HotChocolate][FxMap.HotChocolate.nuget]               | FxMap.HotChocolate is an integration package with HotChocolate for FxMap.                                   | 8.0, 9.0, 10.0 | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.HotChocolate/README.md) |
| **Transports**                                               |                                                                                                             |
| [FxMap.Grpc][FxMap.Grpc.nuget]                               | FxMap.Grpc is an extension package for FxMap that leverages gRPC for efficient data transportation.         | 8.0, 9.0, 10.0 | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Grpc/README.md)         |
| [FxMap.Kafka][FxMap.Kafka.nuget]                             | FxMap.Kafka is an extension package for FxMap that leverages Kafka for efficient data transportation.       | 8.0, 9.0, 10.0 | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Kafka/README.md)        |
| [FxMap.Nats][FxMap.Nats.nuget]                               | FxMap.Nats is an extension package for FxMap that leverages Nats for efficient data transportation.         | 8.0, 9.0, 10.0 | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Nats/README.md)         |
| [FxMap.RabbitMq][FxMap.RabbitMq.nuget]                       | FxMap.RabbitMq is an extension package for FxMap that leverages RabbitMq for efficient data transportation. | 8.0, 9.0, 10.0 | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.RabbitMq/README.md)     |

---

[FxMap.nuget]: https://www.nuget.org/packages/FxMap/

[FxMap.EntityFrameworkCore.nuget]: https://www.nuget.org/packages/FxMap.EntityFrameworkCore/

[FxMap.MongoDb.nuget]: https://www.nuget.org/packages/FxMap.MongoDb/

[FxMap.HotChocolate.nuget]: https://www.nuget.org/packages/FxMap.HotChocolate/

[FxMap.Grpc.nuget]: https://www.nuget.org/packages/FxMap.Grpc/

[FxMap.Nats.nuget]: https://www.nuget.org/packages/FxMap.Nats/

[FxMap.RabbitMq.nuget]: https://www.nuget.org/packages/FxMap.RabbitMq/

[FxMap.Kafka.nuget]: https://www.nuget.org/packages/FxMap.Kafka/
