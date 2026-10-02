<p align="center">
  <img src="https://raw.githubusercontent.com/quyvu01/FxMap/main/FxMap.png" alt="FxMap" width="96" />
</p>

<h1 align="center">FxMap.HotChocolate</h1>

<p align="center">
  HotChocolate integration for FxMap — GraphQL fields declared in a profile are resolved through FxMap.
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/FxMap.HotChocolate"><img alt="NuGet" src="https://img.shields.io/nuget/v/FxMap.HotChocolate.svg" /></a>
  <a href="https://www.nuget.org/packages/FxMap.HotChocolate"><img alt="Downloads" src="https://img.shields.io/nuget/dt/FxMap.HotChocolate.svg" /></a>
  <a href="https://github.com/quyvu01/FxMap/blob/main/LICENSE"><img alt="License" src="https://img.shields.io/badge/license-Apache--2.0-blue.svg" /></a>
</p>

<p align="center">
  <a href="https://github.com/quyvu01/FxMap"><b>FxMap</b></a> ·
  <a href="https://fxmapper.net"><b>Documentation</b></a> ·
  <a href="https://github.com/quyvu01/TestFxMap-Demo"><b>Demo project</b></a>
</p>

---

With **FxMap.HotChocolate**, a GraphQL type whose properties are declared in a `ProfileOf<T>` gets those fields resolved
through FxMap, across services and transports, instead of writing a resolver per field. See the
[Hot Chocolate docs](https://chillicream.com/docs/hotchocolate/v15) for the GraphQL side.

## Installation

```bash
dotnet add package FxMap.HotChocolate
```

> [!WARNING]
> All FxMap.* packages must use the same version.

## Usage

```csharp
var registerBuilder = builder.Services.AddGraphQLServer()
    .AddQueryType<Query>();

builder.Services.AddFxMap(cfg =>
    {
        cfg.AddEntitiesFromAssemblyContaining<SomeEntityAssemblyMarker>();
        cfg.AddProfilesFromAssemblyContaining<SomeProfileAssemblyMarker>();
        cfg.AddNats(config => config.NatsOpts(opts => opts.Url = "nats://localhost:4222"));
    })
    .AddHotChocolate(cfg => cfg.AddRequestExecutorBuilder(registerBuilder));

var app = builder.Build();
app.MapGraphQL();
app.Run();
```

> [!NOTE]
> FxMap.HotChocolate creates an `ObjectTypeExtension<T>` for each response type that has a profile. If you want to
> define the GraphQL type of such an object (for example `UserResponse`) yourself, use `ObjectTypeExtension<T>` instead
> of `ObjectType<T>`.

## Related packages

| Package | Description | Documentation |
|---|---|---|
| **Core** | | |
| [FxMap](https://www.nuget.org/packages/FxMap) | FxMap core: profiles, entity configs, the mapper and the expression language | [README](https://github.com/quyvu01/FxMap/blob/main/README.md) |
| **Data providers** | | |
| [FxMap.EntityFrameworkCore](https://www.nuget.org/packages/FxMap.EntityFrameworkCore) | Answers requests from an Entity Framework Core `DbContext` | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.EntityFrameworkCore/README.md) |
| [FxMap.MongoDb](https://www.nuget.org/packages/FxMap.MongoDb) | Answers requests from MongoDB collections | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.MongoDb/README.md) |
| **Integrations** | | |
| [FxMap.HotChocolate](https://www.nuget.org/packages/FxMap.HotChocolate) | Fills GraphQL response types through HotChocolate | This document |
| **Transports** | | |
| [FxMap.Grpc](https://www.nuget.org/packages/FxMap.Grpc) | gRPC transport | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Grpc/README.md) |
| [FxMap.Nats](https://www.nuget.org/packages/FxMap.Nats) | NATS transport | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Nats/README.md) |
| [FxMap.RabbitMq](https://www.nuget.org/packages/FxMap.RabbitMq) | RabbitMQ transport | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.RabbitMq/README.md) |
| [FxMap.Kafka](https://www.nuget.org/packages/FxMap.Kafka) | Apache Kafka transport | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Kafka/README.md) |
| [FxMap.Azure.ServiceBus](https://www.nuget.org/packages/FxMap.Azure.ServiceBus) | Azure Service Bus transport (Standard / Premium tiers) | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Azure.ServiceBus/README.md) |
| [FxMap.Aws.Sqs](https://www.nuget.org/packages/FxMap.Aws.Sqs) | Amazon SQS transport | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Aws.Sqs/README.md) |
| **Tooling** | | |
| [FxMap.Analyzers](https://www.nuget.org/packages/FxMap.Analyzers) | Roslyn analyzer that validates expression strings at compile time | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Analyzers/README.md) |
