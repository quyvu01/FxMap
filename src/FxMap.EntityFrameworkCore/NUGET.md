![FxMap](https://raw.githubusercontent.com/quyvu01/FxMap/main/docs/assets/FxMap-128.png)

# FxMap.EntityFrameworkCore

Entity Framework Core data provider for FxMap — answers FxMap requests straight from your `DbContext`.

[**FxMap**](https://github.com/quyvu01/FxMap) · [**Documentation**](https://fxmapper.net) · [**Demo project**](https://github.com/quyvu01/TestFxMap-Demo)

---

The owning service of an entity registers it with `EntityConfigureOf<T>` and lists its `DbContext`s. FxMap then turns
incoming requests (ids plus expressions) into one EF Core query per distributed key and returns only the requested values.

## Installation

```bash
dotnet add package FxMap.EntityFrameworkCore
```

> **Warning**
>
> All FxMap.* packages must use the same version.

## Usage

```csharp
public sealed class UserDistributedKey : IDistributedKey;

// 1. Describe the entity: its id, default property and distributed key
public class UserConfig : EntityConfigureOf<User>
{
    protected override void Configure()
    {
        Id(x => x.Id);
        DefaultProperty(x => x.Name);
        UseDistributedKey<UserDistributedKey>();
    }
}

// 2. Register FxMap and the DbContext(s) that hold your entities
builder.Services.AddFxMap(cfg =>
    {
        cfg.AddEntitiesFromAssemblyContaining<SomeEntityAssemblyMarker>();
        cfg.AddProfilesFromAssemblyContaining<SomeProfileAssemblyMarker>();
    })
    .AddEntityFrameworkCore(options =>
        options.AddDbContexts(typeof(AppDbContext), typeof(ReportingDbContext)));
```

`AddEntitiesFromAssemblyContaining` (or `AddEntityConfig<T>`) must run before `AddEntityFrameworkCore`.

## How requests are executed

- **One query per request.** All ids and all expressions of a request are answered by a single `AsNoTracking` query with
  a projection, using the expression language (navigation, filters, aggregations, projections, indexers).
- **Several `DbContext`s are supported.** FxMap routes each entity to the `DbContext` that exposes it as a `DbSet`.
- **Each query runs in its own DI scope**, with its own `DbContext`, so requests can run in parallel safely. It does not
  see uncommitted data held by the caller's `DbContext` or transaction.
- **Ids** are parsed to the entity's id type: `string`, `Guid`, `int`, `long`, `short`, `byte`, `decimal`, `double`,
  `float` and their unsigned / nullable forms, or any strongly typed id with an `IStronglyTypeConverter<TId>` registered.
  Ids that cannot be parsed are ignored.
- **Ids are answered the way they were sent.** `"ABCD…"`, `{…}` or `N`-format Guids, `"007"` for `7` or `" 7 "` are
  found and matched back to the object that sent them (2.3.5+).
- **Aggregates over empty collections**: `:count` and `:sum` return `0`; `:min`, `:max` and `:avg` return `null` (2.3.5+).
- `ExposedName(x => x.Email, "UserEmail")` renames a property in expressions; the original name no longer resolves.

## Related packages

| Package | Description | Documentation |
|---|---|---|
| **Core** | | |
| [FxMap](https://www.nuget.org/packages/FxMap) | FxMap core: profiles, entity configs, the mapper and the expression language | [README](https://github.com/quyvu01/FxMap/blob/main/README.md) |
| **Data providers** | | |
| [FxMap.EntityFrameworkCore](https://www.nuget.org/packages/FxMap.EntityFrameworkCore) | Answers requests from an Entity Framework Core `DbContext` | This document |
| [FxMap.MongoDb](https://www.nuget.org/packages/FxMap.MongoDb) | Answers requests from MongoDB collections | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.MongoDb/README.md) |
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
