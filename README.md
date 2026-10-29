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
  <a href="https://fxmapper.net/docs/quick-start/"><b>Getting Started</b></a> ·
  <a href="https://fxmapper.net/docs/expressions"><b>Expression Language</b></a> ·
  <a href="https://discord.gg/XJNzPbqg7"><b>Discord</b></a>
</p>

---

<p align="center">
  <img src="docs/assets/fxmap-hero.gif" alt="FxMap fills the UserName and UserEmail of an order from the User service in one call" width="900" />
</p>

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

To compare it with the hand-written versions, [sample/ManualVsFxMap](sample/ManualVsFxMap) fills 100 orders one call per
order, with a hand-written batch, and with FxMap, and shows the request counts and what each does when the user service
is down or slow.

## Also in Java and Node.js

FxMap speaks one wire format, so services written in different languages can map each other's data:

| Language | Library | Docs |
|---|---|---|
| .NET 8, 9, 10 | **FxMap** (this repo) | [fxmapper.net](https://fxmapper.net) |
| Java 21, Spring Boot 4 | [jfxmap](https://github.com/quyvu01/jfxmap) | [j.fxmapper.net](https://j.fxmapper.net) |
| Node.js 20+, TypeScript | [nfxmap](https://github.com/quyvu01/nfxmap) | [n.fxmapper.net](https://n.fxmapper.net) |

A .NET service can fill its DTOs from a Java or Node.js service, and the other way around. To see it run, [dotnet-java-nats](https://github.com/quyvu01/jfxmap/tree/main/samples/dotnet-java-nats) starts a .NET service on FxMap and EF Core and a Java service on jfxmap and JPA that map each other's data over NATS, with one script.

## Features

- **FluentAPI mapping**: declare the mapping with `ProfileOf<T>` and `EntityConfigureOf<T>`; no attributes on your DTOs, and property access is compiled once per type.
- **Expression language**: a SQL-like DSL for navigation, filters (`:where`), ordering and paging (`:orderBy`, `:offset`, `:limit`, `:first`, `:last`), aggregations, projections and conditions (`Orders:where(Status = 'Done'):sum(Total)`).
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
            .For(x => x.CompletedOrders, "Orders:where(Status = 'Done')")
            // Aggregation
            .For(x => x.TotalSpent, "Orders:sum(Total)")
            // Projection
            .For(x => x.UserDetails, "{Id, Name, Address.City as CityName}")
            // GroupBy
            .For(x => x.OrdersByStatus, "Orders:groupBy(Status).{Status, :count as Count}");
    }
}
```

For complete expression syntax including filters, ordering and paging, functions, aggregations, boolean functions, coalesce,
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

FxMap is built to enrich data that lives in other services, so most of a real call is spent waiting on the network. This benchmark removes the network to measure only the mapper itself: FxMap enriches DTOs from an EF Core database in the same process (no transport), compared with AutoMapper 14 `ProjectTo` doing a single projected query. It runs on two EF Core providers, because they behave very differently.

**Scenario:** each order has a customer (customer → province → country) and 3-5 items (item → product → category). Both sides produce the same `OrderDto` graph (the benchmark checks that the results are identical).

- **AutoMapper:** `db.Orders.ProjectTo<OrderDto>(config)`. On a relational provider this is one SQL query with joins. The InMemory provider builds no SQL at all, so there "one query" is not a join.
- **FxMap:** load the DTOs with only their scalar values and keys, then `IDistributedMapper.MapDataAsync(dtos)` resolves customer, province, country, product and category through `ProfileOf<T>` and the EF Core data provider.

### Relational (SQLite in-memory, real joins)

| Orders | AutoMapper `ProjectTo` | FxMap (query + enrich) | FxMap time vs AutoMapper | FxMap memory vs AutoMapper |
|-------:|-----------------------:|-----------------------:|-------------------------:|---------------------------:|
| 10     | 303 µs / 114 KB        | 1.08 ms / 587 KB       | 3.57×                    | 5.13×                      |
| 100    | 2.79 ms / 540 KB       | 3.29 ms / 1.5 MB       | 1.18×                    | 2.71×                      |
| 1,000  | 21.4 ms / 4.8 MB       | 21.3 ms / 7.7 MB       | 1.00×                    | 1.59×                      |

### InMemory provider (no SQL)

| Orders | AutoMapper `ProjectTo` | FxMap (query + enrich) | FxMap time vs AutoMapper | FxMap memory vs AutoMapper |
|-------:|-----------------------:|-----------------------:|-------------------------:|---------------------------:|
| 10     | 181 µs / 276 KB        | 440 µs / 387 KB        | 2.44×                    | 1.40×                      |
| 100    | 2.25 ms / 2.2 MB       | 2.19 ms / 1.1 MB       | 0.97×                    | 0.52×                      |
| 1,000  | 101 ms / 21.4 MB       | 98.7 ms / 7.4 MB       | 0.98×                    | 0.35×                      |

BenchmarkDotNet 0.15.6, 1 launch, 3 warmups, 10 iterations, .NET 10, Apple M1 Pro. Lower is better. Single-launch numbers are noisy, especially for small batches.

How to read it:

- Against a real joined query, FxMap is not faster than `ProjectTo`: it is slower for small batches, on par at about 1,000 orders, and allocates more memory. Do not pick FxMap for speed inside a single database.
- The memory advantage in the InMemory table does not carry over to a relational provider, because there `ProjectTo` materializes far less than it does on InMemory.
- The fixed cost of one scoped query per distributed key and level is why small batches are slower in relative terms.
- FxMap's value is elsewhere: the data a DTO needs lives in another service or database, where a join is not possible. Then the cost is round trips, and FxMap batches them by key. See [`sample/ManualVsFxMap`](sample/ManualVsFxMap), where 100 orders take 100 requests with a per-order call and 1 with FxMap (a hand-written batch endpoint also takes 1).
- This is a local, single-process comparison. These numbers say nothing about network latency.

To reproduce:

```bash
cd FxMap/test/FxMap.Benchmark
dotnet run -c Release -- --filter '*.ProjectionBenchmark.*' '*.ProjectionSqliteBenchmark.*'
```

## Documentation

Visit **[fxmapper.net](https://fxmapper.net)** for:

- [Getting Started Guide](https://fxmapper.net/docs/quick-start/)
- [Configuration Options](https://fxmapper.net/docs/configuration/service-registration/)
- [Expression Language Reference](https://fxmapper.net/docs/expressions)
- [Data Provider Setup](https://fxmapper.net/docs/data-providers/)
- [Transport Configuration](https://fxmapper.net/docs/transports)

## Community

If FxMap saves you glue code, a ⭐ on GitHub helps other developers find it.

- Chat and get help on **[Discord](https://discord.gg/XJNzPbqg7)**
- Ask questions and share ideas in [GitHub Discussions](https://github.com/quyvu01/FxMap/discussions)
- Coming from OfX? Read the [migration guide](https://fxmapper.net/docs/migrate-from-ofx/)
- Adopting FxMap at your company and want hands-on help? Open a thread in [GitHub Discussions](https://github.com/quyvu01/FxMap/discussions) or ask on Discord.

## Contributing

Contributions are welcome! See [CONTRIBUTING.md](CONTRIBUTING.md) or visit our [GitHub repository](https://github.com/quyvu01/FxMap) to:

- Report issues
- Submit pull requests
- Request features

## License

This project is licensed under the Apache-2.0 license.

---
