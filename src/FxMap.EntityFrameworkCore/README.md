# FxMap-EFCore

FxMap-EFCore is an extension package for FxMap that integrates with Entity Framework Core to simplify data fetching by
leveraging attribute-based data mapping. This extension streamlines data retrieval using EF Core, reducing boilerplate
code and improving maintainability.

[Demo Project!](https://github.com/quyvu01/TestFxMap-Demo)

---

## Introduction

FxMap-EFCore extends the core FxMap library by providing seamless integration with Entity Framework Core. This enables
developers to automatically map and retrieve data directly from a database, leveraging the power of Entity Framework
Core along with attribute-based data mapping.

For example, suppose you have a `UserId` property in your model, and you want to fetch the corresponding `Name`
and `Email` fields from the database. By using FxMap-EFCore, you can annotate your model with attributes, and the library
will handle data fetching for you.

---

## Installation

To install the FxMap-EFCore package, use the following NuGet command:

```bash
dotnet add package FxMap-EFCore
```

Or via the NuGet Package Manager:

```bash
Install-Package FxMap-EFCore
```

---

## How to Use

### 1. Register FxMap-EfCore

Add FxMap-EfCore to your service configuration during application startup:

```csharp
builder.Services.AddFxMapEntityFrameworkCore(cfg =>
{
    cfg.AddAttributesContainNamespaces(typeof(WhereTheAttributeDefined).Assembly);
    cfg.AddHandlersFromNamespaceContaining<SomeHandlerAssemblyMarker>();
})
.AddFxMapEFCore(options =>
{
    options.AddDbContexts(typeof(TestDbContext), typeof(OtherDbContext)...);
});
```

### Function Descriptions

#### AddDbContexts

Here, you can use the method `AddDbContexts()`, which takes `DbContext(s)` to executing.

That all, Enjoy your moment!

| Package Name                               | Description                                                                                             | .NET Version | Document                                                                          |
|--------------------------------------------|---------------------------------------------------------------------------------------------------------|--------------|-----------------------------------------------------------------------------------|
| **Core**                                   |                                                                                                         |
| [FxMap][FxMap.nuget]                           | FxMap core                                                                                                | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/README.md)                      |
| **Data Providers**                         |                                                                                                         |
| [FxMap-EFCore][FxMap-EFCore.nuget]             | This is the FxMap extension package using EntityFramework to fetch data                                   | 8.0, 9.0     | This Document                                                                     |
| [FxMap-MongoDb][FxMap-MongoDb.nuget]           | This is the FxMap extension package using MongoDb to fetch data                                           | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.MongoDb/README.md)      |
| **Integrations**                           |                                                                                                         |
| [FxMap-HotChocolate][FxMap-HotChocolate.nuget] | FxMap.HotChocolate is an integration package with HotChocolate for FxMap.                                   | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.HotChocolate/README.md) |
| **Transports**                             |                                                                                                         |
| [FxMap-gRPC][FxMap-gRPC.nuget]                 | FxMap.gRPC is an extension package for FxMap that leverages gRPC for efficient data transportation.         | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Grpc/README.md)         |
| [FxMap-Kafka][FxMap-Kafka.nuget]               | FxMap-Kafka is an extension package for FxMap that leverages Kafka for efficient data transportation.       | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Kafka/README.md)        |
| [FxMap-Nats][FxMap-Nats.nuget]                 | FxMap-Nats is an extension package for FxMap that leverages Nats for efficient data transportation.         | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Nats/README.md)         |
| [FxMap-RabbitMq][FxMap-RabbitMq.nuget]         | FxMap-RabbitMq is an extension package for FxMap that leverages RabbitMq for efficient data transportation. | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.RabbitMq/README.md)     |

---

[FxMap.nuget]: https://www.nuget.org/packages/FxMap/

[FxMap-EFCore.nuget]: https://www.nuget.org/packages/FxMap-EFCore/

[FxMap-MongoDb.nuget]: https://www.nuget.org/packages/FxMap-MongoDb/

[FxMap-HotChocolate.nuget]: https://www.nuget.org/packages/FxMap-HotChocolate/

[FxMap-gRPC.nuget]: https://www.nuget.org/packages/FxMap-gRPC/

[FxMap-Nats.nuget]: https://www.nuget.org/packages/FxMap-Nats/

[FxMap-RabbitMq.nuget]: https://www.nuget.org/packages/FxMap-RabbitMq/

[FxMap-Kafka.nuget]: https://www.nuget.org/packages/FxMap-Kafka/