<p align="center">
  <img src="https://raw.githubusercontent.com/quyvu01/FxMap/main/FxMap.png" alt="FxMap" width="96" />
</p>

<h1 align="center">FxMap.Azure.ServiceBus</h1>

<p align="center">
  Azure Service Bus transport for FxMap — request/reply between services over session-enabled queues.
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/FxMap.Azure.ServiceBus"><img alt="NuGet" src="https://img.shields.io/nuget/v/FxMap.Azure.ServiceBus.svg" /></a>
  <a href="https://www.nuget.org/packages/FxMap.Azure.ServiceBus"><img alt="Downloads" src="https://img.shields.io/nuget/dt/FxMap.Azure.ServiceBus.svg" /></a>
  <a href="https://github.com/quyvu01/FxMap/blob/main/LICENSE"><img alt="License" src="https://img.shields.io/badge/license-Apache--2.0-blue.svg" /></a>
</p>

<p align="center">
  <a href="https://github.com/quyvu01/FxMap"><b>FxMap</b></a> ·
  <a href="https://fxmapper.net"><b>Documentation</b></a> ·
  <a href="https://github.com/quyvu01/TestFxMap-Demo"><b>Demo project</b></a>
</p>

---

> [!WARNING]
> This transport requires the **Standard** or **Premium** tier of Azure Service Bus. Premium is recommended for production.

A transport carries FxMap requests between the service that needs data (the client) and the service that owns it
(the server). Every service that maps data needs the core `FxMap` package; the owning service also needs a data provider
([EntityFrameworkCore](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.EntityFrameworkCore/README.md) or
[MongoDb](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.MongoDb/README.md)), and both sides use the same transport.
Requests are batched per distributed key, so a hundred objects that need a `UserName` produce one request.

## Installation

```bash
dotnet add package FxMap.Azure.ServiceBus
```

> [!WARNING]
> All FxMap.* packages must use the same version.

## Usage

```csharp
builder.Services.AddFxMap(cfg =>
{
    cfg.AddEntitiesFromAssemblyContaining<SomeEntityAssemblyMarker>();
    cfg.AddProfilesFromAssemblyContaining<SomeProfileAssemblyMarker>();
    cfg.AddAzureServiceBus(c =>
    {
        c.Host("Endpoint=sb://...");   // connection string
        c.TopicPrefix("staging");       // optional
        c.MaxConcurrentSessions(16);    // optional, at least 1
    });
});
```

## Naming and sessions

For every distributed key FxMap creates **session-enabled** queues at startup: `fxmap-request-{namespace}-{type name}`
for requests and `fxmap-reply-{namespace}-{type name}` for replies (dots replaced by hyphens, lower case). `TopicPrefix` prepends `"{prefix}-"` to both. Avoid using these names for anything
else.

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
| [FxMap.Kafka](https://www.nuget.org/packages/FxMap.Kafka) | Apache Kafka transport | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Kafka/README.md) |
| [FxMap.Azure.ServiceBus](https://www.nuget.org/packages/FxMap.Azure.ServiceBus) | Azure Service Bus transport (Standard / Premium tiers) | This document |
| [FxMap.Aws.Sqs](https://www.nuget.org/packages/FxMap.Aws.Sqs) | Amazon SQS transport | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Aws.Sqs/README.md) |
| **Tooling** | | |
| [FxMap.Analyzers](https://www.nuget.org/packages/FxMap.Analyzers) | Roslyn analyzer that validates expression strings at compile time | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Analyzers/README.md) |
