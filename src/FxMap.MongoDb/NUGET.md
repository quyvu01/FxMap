# FxMap.MongoDb

MongoDB data provider for FxMap — answers FxMap requests straight from your collections.

[**FxMap**](https://github.com/quyvu01/FxMap) · [**Documentation**](https://fxmapper.net) · [**Demo project**](https://github.com/quyvu01/TestFxMap-Demo)

---

The owning service of an entity registers it with `EntityConfigureOf<T>` and hands FxMap the `IMongoCollection<T>` that
stores it. Incoming requests (ids plus expressions) become one MongoDB query with a BSON projection per distributed key.

## Installation

```bash
dotnet add package FxMap.MongoDb
```

> **Warning**
>
> All FxMap.* packages must use the same version.

## Usage

```csharp
public sealed class UserDistributedKey : IDistributedKey;

public class UserConfig : EntityConfigureOf<User>
{
    protected override void Configure()
    {
        Id(x => x.Id);
        DefaultProperty(x => x.Name);
        UseDistributedKey<UserDistributedKey>();
    }
}

builder.Services.AddFxMap(cfg =>
    {
        cfg.AddEntitiesFromAssemblyContaining<SomeEntityAssemblyMarker>();
        cfg.AddProfilesFromAssemblyContaining<SomeProfileAssemblyMarker>();
    })
    .AddMongoDb(cfg => cfg
        .AddCollection(database.GetCollection<User>("users"))
        .AddCollection(database.GetCollection<Order>("orders")));
```

## Notes

- `AddCollection` can be chained; register one collection per entity that FxMap should serve.
- Expressions are evaluated against the stored documents, so they use the **actual property names** of the model
  (not an `ExposedName`).
- Ids are parsed to the entity's id type through the same converters as the other providers, including strongly typed
  ids with an `IStronglyTypeConverter<TId>`. As of 2.3.5 a response is also answered under the id text that was sent.
  That match compares the text of the stored `_id` with the text of the parsed id, so an id type whose BSON text form
  differs from `ToString()` is not matched. Test your id type.

## Related packages

| Package | Description | Documentation |
|---|---|---|
| **Core** | | |
| [FxMap](https://www.nuget.org/packages/FxMap) | FxMap core: profiles, entity configs, the mapper and the expression language | [README](https://github.com/quyvu01/FxMap/blob/main/README.md) |
| **Data providers** | | |
| [FxMap.EntityFrameworkCore](https://www.nuget.org/packages/FxMap.EntityFrameworkCore) | Answers requests from an Entity Framework Core `DbContext` | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.EntityFrameworkCore/README.md) |
| [FxMap.MongoDb](https://www.nuget.org/packages/FxMap.MongoDb) | Answers requests from MongoDB collections | This document |
| **Integrations** | | |
| [FxMap.HotChocolate](https://www.nuget.org/packages/FxMap.HotChocolate) | Fills GraphQL response types through HotChocolate | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.HotChocolate/README.md) |
| **Transports** | | |
| [FxMap.Grpc](https://www.nuget.org/packages/FxMap.Grpc) | gRPC transport | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Grpc/README.md) |
| [FxMap.Nats](https://www.nuget.org/packages/FxMap.Nats) | NATS transport | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Nats/README.md) |
| [FxMap.RabbitMq](https://www.nuget.org/packages/FxMap.RabbitMq) | RabbitMQ transport | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.RabbitMq/README.md) |
| [FxMap.Kafka](https://www.nuget.org/packages/FxMap.Kafka) | Apache Kafka transport | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Kafka/README.md) |
| [FxMap.Azure.ServiceBus](https://www.nuget.org/packages/FxMap.Azure.ServiceBus) | Azure Service Bus transport (Standard / Premium tiers) | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Azure.ServiceBus/README.md) |
| [FxMap.Aws.Sqs](https://www.nuget.org/packages/FxMap.Aws.Sqs) | Amazon SQS transport | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Aws.Sqs/README.md) |
| **Tooling** | | |
| [FxMap.Analyzers](https://www.nuget.org/packages/FxMap.Analyzers) | Roslyn analyzer that validates expression strings at compile time | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Analyzers/README.md) |
