# FxMap.Kafka

Apache Kafka transport for FxMap — request/response between services over Kafka topics.

[**FxMap**](https://github.com/quyvu01/FxMap) · [**Documentation**](https://fxmapper.net) · [**Demo project**](https://github.com/quyvu01/TestFxMap-Demo)

---

A transport carries FxMap requests between the service that needs data (the client) and the service that owns it
(the server). Every service that maps data needs the core `FxMap` package; the owning service also needs a data provider
([EntityFrameworkCore](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.EntityFrameworkCore/README.md) or
[MongoDb](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.MongoDb/README.md)), and both sides use the same transport.
Requests are batched per distributed key, so a hundred objects that need a `UserName` produce one request.

## Installation

```bash
dotnet add package FxMap.Kafka
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
    cfg.AddKafka(c => c.Host("localhost:9092"));
});
```

`Ssl(...)` configures TLS for the connection. A process shares one Kafka producer between its client and its server.

### Partitions and replication

```csharp
cfg.AddKafka(c =>
{
    c.Host("localhost:9092");
    c.Partitions(6);          // partitions of the request topic of each distributed key (default 1)
    c.ReplicationFactor(3);   // replication factor of the topics FxMap creates (default 1)
});
```

The requests are spread over the partitions and the instances of a service share them as one consumer group, so a topic needs at least as
many partitions as the instances that should work at the same time. FxMap only sets these values when it **creates** a topic: a topic that
already exists is left as it is (change it with the tools of Kafka). The reply topic of a client is read by one consumer, so it has one
partition. A cluster that has to survive the loss of a broker needs a replication factor of 3 (and not more than the number of brokers).

## Naming

Requests go to the topic `fxmap-request-topic-{distributed key full name}` (lower case), which FxMap creates for you.
Each application listens for responses on `fxmap-response-topic-[application.friendly.name]`. Avoid using these topics
for anything else.

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
| [FxMap.RabbitMq](https://www.nuget.org/packages/FxMap.RabbitMq) | RabbitMQ transport | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.RabbitMq/README.md) |
| [FxMap.Kafka](https://www.nuget.org/packages/FxMap.Kafka) | Apache Kafka transport | This document |
| [FxMap.Azure.ServiceBus](https://www.nuget.org/packages/FxMap.Azure.ServiceBus) | Azure Service Bus transport (Standard / Premium tiers) | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Azure.ServiceBus/README.md) |
| [FxMap.Aws.Sqs](https://www.nuget.org/packages/FxMap.Aws.Sqs) | Amazon SQS transport | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Aws.Sqs/README.md) |
| **Tooling** | | |
| [FxMap.Analyzers](https://www.nuget.org/packages/FxMap.Analyzers) | Roslyn analyzer that validates expression strings at compile time | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Analyzers/README.md) |
