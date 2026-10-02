<p align="center">
  <img src="https://raw.githubusercontent.com/quyvu01/FxMap/main/FxMap.png" alt="FxMap" width="120" />
</p>

<h1 align="center">FxMap</h1>

<p align="center">
  Distributed data mapping for .NET — declare where a property comes from, and FxMap fetches it from the service
  that owns the data, over the transport you already run.
</p>

<p align="center">
  <a href="https://www.nuget.org/packages/FxMap"><img alt="NuGet" src="https://img.shields.io/nuget/v/FxMap.svg" /></a>
  <a href="https://www.nuget.org/packages/FxMap"><img alt="Downloads" src="https://img.shields.io/nuget/dt/FxMap.svg" /></a>
  <a href="https://github.com/quyvu01/FxMap/actions/workflows/build.yml"><img alt="Build" src="https://github.com/quyvu01/FxMap/actions/workflows/build.yml/badge.svg" /></a>
  <a href="LICENSE"><img alt="License" src="https://img.shields.io/badge/license-Apache--2.0-blue.svg" /></a>
  <img alt=".NET" src="https://img.shields.io/badge/.NET-8%20%7C%209%20%7C%2010-512BD4.svg" />
</p>

<p align="center">
  <a href="https://fxmapper.net"><b>Documentation</b></a> ·
  <a href="https://fxmapper.net/docs/getting-started"><b>Getting Started</b></a> ·
  <a href="https://fxmapper.net/docs/expressions"><b>Expression Language</b></a>
</p>

---

In a microservice system a response often carries only keys: an order has a `UserId`, a `ProvinceId`, a `ProductId`.
Showing it means collecting `UserName`, `ProvinceName`, `ProductName` from the services that own them, and writing
that glue again for every endpoint. FxMap turns the glue into a declaration:

```csharp
public class OrderResponse
{
    public string UserId { get; set; }
    public string UserName { get; set; }     // filled by FxMap
    public string UserEmail { get; set; }    // filled by FxMap
}

public class OrderResponseProfile : ProfileOf<OrderResponse>
{
    protected override void Configure() =>
        UseDistributedKey<UserDistributedKey>()
            .Of(x => x.UserId)
            .For(x => x.UserName)
            .For(x => x.UserEmail, "Email");
}

await distributedMapper.MapDataAsync(response);   // one batched request per key, however many objects
```

The service that owns the data registers an entity configuration and a data provider. FxMap batches the ids,
sends them over a transport (or answers locally when the owner is the same service), and writes the results back.
No client code per field, and no coupling between the two services beyond a key name.

## Features

- **FluentAPI mapping**: declare the mapping with `ProfileOf<T>` and `EntityConfigureOf<T>`; no attributes on your DTOs, and property access is compiled once per type.
- **Expression language**: a SQL-like DSL for navigation, filters, aggregations, projections, indexers and conditions (`Orders(Status = 'Done'):sum(Total)`).
- **Batched and chained**: one request per distributed key and dependency level, with distinct ids, however many objects are mapped; chains such as `UserId → ProvinceId → CountryId` resolve level by level.
- **Data providers**: Entity Framework Core and MongoDB.
- **Transports**: gRPC, NATS, RabbitMQ, Kafka, Azure Service Bus and Amazon SQS, with retries and supervision.
- **GraphQL integration** with HotChocolate.
- **Compile-time checks**: Roslyn analyzers for misconfigured profiles and entities.
- **Observable**: activities for OpenTelemetry tracing on mapping and data access.
- Targets **.NET 8, 9 and 10**.

> [!WARNING]
> All FxMap.* packages need to have the same version.

## Quick Start

```bash
dotnet add package FxMap
```

```csharp
// 1. Configure FxMap
builder.Services.AddFxMap(cfg =>
{
    cfg.AddEntitiesFromAssemblyContaining<SomeEntityAssemblyMarker>();
    cfg.AddProfilesFromAssemblyContaining<SomeProfileAssemblyMarker>();
});

// 2. Define a distributed key
public sealed class UserDistributedKey : IDistributedKey;

// 3. Configure the entity with FluentAPI
public class UserConfig : EntityConfigureOf<User>
{
    protected override void Configure()
    {
        Id(x => x.Id);
        DefaultProperty(x => x.Name);
        UseDistributedKey<UserDistributedKey>(); // Or you want to absolute lose coupling, you can use: UseDistributedKey("UserDistributedKey")
        ExposedName(x => x.Email, "UserEmail");
    }
}

// 4. Define a profile for your DTO
public class UserResponseProfile : ProfileOf<UserResponse>
{
    protected override void Configure()
    {
        UseDistributedKey<UserDistributedKey>() // Or you want to absolute lose coupling, you can use: UseDistributedKey("UserDistributedKey")
            .Of(x => x.UserId)
            .For(x => x.UserName)
            .For(x => x.UserEmail, "Email");
    }
}
```

## Expression Examples

```csharp
public class UserResponseProfile : ProfileOf<UserResponse>
{
    protected override void Configure()
    {
        UseDistributedKey<UserDistributedKey>()
            .Of(x => x.UserId)
            // Simple property access
            .For(x => x.UserEmail, "Email")
            // Navigation properties
            .For(x => x.CountryName, "Country.Name")
            // Filtering
            .For(x => x.CompletedOrders, "Orders(Status = 'Done')")
            // Aggregation
            .For(x => x.TotalSpent, "Orders:sum(Total)")
            // Projection
            .For(x => x.UserDetails, "{Id, Name, Address.City as CityName}")
            // GroupBy
            .For(x => x.OrdersByStatus, "Orders:groupBy(Status).{Status, :count as Count}");
    }
}
```

For complete expression syntax including filters, indexers, functions, aggregations, boolean functions, coalesce,
ternary operators, and more, visit **[Expression Documentation](https://fxmapper.net/docs/expressions)**.

## Packages

| Package | Description | .NET | Documentation |
|---|---|---|---|
| **Core** | | | |
| [FxMap](https://www.nuget.org/packages/FxMap) | FxMap core: profiles, entity configs, the mapper and the expression language | 8.0, 9.0, 10.0 | [fxmapper.net](https://fxmapper.net) |
| **Data providers** | | | |
| [FxMap.EntityFrameworkCore](https://www.nuget.org/packages/FxMap.EntityFrameworkCore) | Answers requests from an Entity Framework Core `DbContext` | 8.0, 9.0, 10.0 | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.EntityFrameworkCore/README.md) |
| [FxMap.MongoDb](https://www.nuget.org/packages/FxMap.MongoDb) | Answers requests from MongoDB collections | 8.0, 9.0, 10.0 | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.MongoDb/README.md) |
| **Integrations** | | | |
| [FxMap.HotChocolate](https://www.nuget.org/packages/FxMap.HotChocolate) | Fills GraphQL response types through HotChocolate | 8.0, 9.0, 10.0 | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.HotChocolate/README.md) |
| **Transports** | | | |
| [FxMap.Grpc](https://www.nuget.org/packages/FxMap.Grpc) | gRPC transport | 8.0, 9.0, 10.0 | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Grpc/README.md) |
| [FxMap.Nats](https://www.nuget.org/packages/FxMap.Nats) | NATS transport | 8.0, 9.0, 10.0 | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Nats/README.md) |
| [FxMap.RabbitMq](https://www.nuget.org/packages/FxMap.RabbitMq) | RabbitMQ transport | 8.0, 9.0, 10.0 | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.RabbitMq/README.md) |
| [FxMap.Kafka](https://www.nuget.org/packages/FxMap.Kafka) | Apache Kafka transport | 8.0, 9.0, 10.0 | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Kafka/README.md) |
| [FxMap.Azure.ServiceBus](https://www.nuget.org/packages/FxMap.Azure.ServiceBus) | Azure Service Bus transport (Standard / Premium tiers) | 8.0, 9.0, 10.0 | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Azure.ServiceBus/README.md) |
| [FxMap.Aws.Sqs](https://www.nuget.org/packages/FxMap.Aws.Sqs) | Amazon SQS transport | 8.0, 9.0, 10.0 | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Aws.Sqs/README.md) |
| **Tooling** | | | |
| [FxMap.Analyzers](https://www.nuget.org/packages/FxMap.Analyzers) | Roslyn analyzer that validates expression strings at compile time | 8.0, 9.0, 10.0 | [README](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Analyzers/README.md) |

## Performance

FxMap is built to enrich data that lives in other services, so most of a real call is spent waiting on the network. This benchmark removes the network to measure only the mapper itself: FxMap enriches DTOs from an EF Core InMemory database in the same process (no transport), compared with AutoMapper 14 `ProjectTo` doing a single projected query.

**Scenario:** each order has a customer (customer → province → country) and 3-5 items (item → product → category). Both sides produce the same `OrderDto` graph (the benchmark checks that the results are identical).

- **AutoMapper:** `db.Orders.ProjectTo<OrderDto>(config)`, one query with joins.
- **FxMap:** load the DTOs with only their scalar values and keys, then `IDistributedMapper.MapDataAsync(dtos)` resolves customer, province, country, product and category through `ProfileOf<T>` and the EF Core data provider.

| Orders | AutoMapper `ProjectTo` | FxMap (query + enrich) | FxMap time vs AutoMapper | FxMap memory vs AutoMapper |
|-------:|-----------------------:|-----------------------:|-------------------------:|---------------------------:|
| 10     | 164 µs / 276 KB        | 277 µs / 382 KB        | 1.69×                    | 1.38×                      |
| 100    | 2.07 ms / 2.2 MB       | **1.81 ms / 1.1 MB**   | **0.88×**                | **0.50×**                  |
| 1,000  | 96.4 ms / 21.4 MB      | **93.6 ms / 7.0 MB**   | **0.97×**                | **0.33×**                  |

BenchmarkDotNet 0.15, 3 launches, .NET 10, Apple M1 Pro. Lower is better.

How to read it:

- Past a few dozen objects FxMap is on par with or faster than a single joined `ProjectTo` and allocates a half to a third of the memory, because it fetches each distinct key once instead of joining every row.
- With a handful of objects both take well under a millisecond; the fixed cost of one scoped query per distributed key and level shows up (about 0.1 ms here) and FxMap is slower in relative terms.
- At 1,000 orders the in-memory query itself is most of the time (about 84 ms of the 94 ms), so the gap between the two is small there.
- This is a local, single-process comparison on an in-memory provider. With a real database or transport, the cost of the round trips dominates and FxMap's batching by key is what matters; these numbers say nothing about network latency.

To reproduce:

```bash
cd FxMap/test/FxMap.Benchmark
dotnet run -c Release -- --filter '*ProjectionBenchmark*' --launchCount 3
```

## Documentation

Visit **[fxmapmapper.net](https://fxmapper.net)** for:

- [Getting Started Guide](https://fxmapper.net/docs/getting-started)
- [Configuration Options](https://fxmapper.net/docs/configuration)
- [Expression Language Reference](https://fxmapper.net/docs/expressions)
- [Data Provider Setup](https://fxmapper.net/docs/providers)
- [Transport Configuration](https://fxmapper.net/docs/transports)
- [API Reference](https://fxmapper.net/docs/api)

## Contributing

Contributions are welcome! Please visit our [GitHub repository](https://github.com/quyvu01/FxMap) to:

- Report issues
- Submit pull requests
- Request features

## License

This project is licensed under the Apache-2.0 license.

---
