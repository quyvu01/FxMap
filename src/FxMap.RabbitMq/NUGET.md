![FxMap](https://raw.githubusercontent.com/quyvu01/FxMap/main/docs/assets/FxMap-128.png)

# FxMap.RabbitMq

RabbitMQ transport for FxMap — request/reply between services over RabbitMQ.

[**FxMap**](https://github.com/quyvu01/FxMap) · [**Documentation**](https://fxmapper.net) · [**Demo project**](https://github.com/quyvu01/TestFxMap-Demo)

---

A transport carries FxMap requests between the service that needs data (the client) and the service that owns it
(the server). Every service that maps data needs the core `FxMap` package; the owning service also needs a data provider
([EntityFrameworkCore](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.EntityFrameworkCore/README.md) or
[MongoDb](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.MongoDb/README.md)), and both sides use the same transport.
Requests are batched per distributed key, so a hundred objects that need a `UserName` produce one request.

## Installation

```bash
dotnet add package FxMap.RabbitMq
```

> **Warning**
>
> All FxMap.* packages must use the same version.

## Usage

```csharp
builder.Services.AddFxMap(cfg =>
{
    cfg.AddEntitiesFromAssemblyContaining<SomeEntityAssemblyMarker>();
    cfg.AddProfilesFromAssemblyContaining<SomeProfileAssemblyMarker>();

    // Local development
    cfg.AddRabbitMq(config => config.Host("localhost", "/"));

    // Or with credentials, port and a larger channel pool
    cfg.AddRabbitMq(config =>
    {
        config.Host("rabbit.internal", "/", 5672, c =>
        {
            c.UserName("SomeUserName");
            c.Password("SomePassword");
        });
        config.ChannelPoolSize(10);
    });
});
```

## Connection and channels

The client and the server of a process share **one** RabbitMQ connection (opened lazily, with automatic recovery) and a
pool of channels (`ChannelPoolSize`, default 5). The server opens one consumer per pooled channel, and concurrency is
driven by RabbitMQ's native `consumerDispatchConcurrency`.

## Naming

FxMap uses exchanges named `FxMap-{namespace}:{type name}` and creates the queue
`fxmap-rpc-queue-[application friendly name]`. Avoid using these names for anything else.

## Related packages

| Package | Description | Documentation |
|---|---|---|
| **Core** | | |
| [FxMap](https://www.nuget.org/packages/FxMap) | FxMap core: profiles, entity configs, the mapper and the expression language | [README](https://github.com/quyvu01/FxMap/blob/main/README.md) |
| **Data providers** | | |
| [FxMap.EntityFrameworkCore](https://www.nuget.org/packages/FxMap.EntityFrameworkCore) | Answers requests from an Entity Framework Core `DbContext` | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.EntityFrameworkCore/README.md) |
| [FxMap.MongoDb](https://www.nuget.org/packages/FxMap.MongoDb) | Answers requests from MongoDB collections | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.MongoDb/README.md) |
| **Integrations** | | |
| [FxMap.HotChocolate](https://www.nuget.org/packages/FxMap.HotChocolate) | Fills GraphQL response types through HotChocolate | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.HotChocolate/README.md) |
| **Transports** | | |
| [FxMap.Grpc](https://www.nuget.org/packages/FxMap.Grpc) | gRPC transport | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Grpc/README.md) |
| [FxMap.Nats](https://www.nuget.org/packages/FxMap.Nats) | NATS transport | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Nats/README.md) |
| [FxMap.RabbitMq](https://www.nuget.org/packages/FxMap.RabbitMq) | RabbitMQ transport | This document |
| [FxMap.Kafka](https://www.nuget.org/packages/FxMap.Kafka) | Apache Kafka transport | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Kafka/README.md) |
| [FxMap.Azure.ServiceBus](https://www.nuget.org/packages/FxMap.Azure.ServiceBus) | Azure Service Bus transport (Standard / Premium tiers) | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Azure.ServiceBus/README.md) |
| [FxMap.Aws.Sqs](https://www.nuget.org/packages/FxMap.Aws.Sqs) | Amazon SQS transport | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Aws.Sqs/README.md) |
| **Tooling** | | |
| [FxMap.Analyzers](https://www.nuget.org/packages/FxMap.Analyzers) | Roslyn analyzer that validates expression strings at compile time | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Analyzers/README.md) |
