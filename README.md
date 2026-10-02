# FxMap
Effective distributed data mapping!
```csharp
public string UserId { get; set; }
public string UserName { get; set; }
public string UserEmail { get; set; }
```

FxMap is an open-source library focused on FluentAPI-based data mapping. It streamlines data handling across services,
reduces boilerplate code, and improves maintainability.

**[Full Documentation](https://fxmapper.net)** | **[Getting Started](https://fxmapper.net/docs/getting-started)** |**[Expression Language](https://fxmapper.net/docs/expressions)**

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

## Key Features

- **FluentAPI-based Mapping**: Declarative data fetching using `ProfileOf<T>` and `EntityConfigureOf<T>`
- **Powerful Expression Language**: SQL-like DSL for complex queries, filtering, aggregation, and projections
- **Multiple Data Providers**: Support for EF Core, MongoDB, and more
- **Multiple Transports**: gRPC, NATS, RabbitMQ, Kafka, Azure Service Bus, Amazon SQS
- **GraphQL Integration**: Seamless integration with HotChocolate

## Expression Examples

```csharp
public class UserResponseProfile : ProfileOf<UserResponse>
{
    protected override void Configure()
    {
        UseDistributedKey<UserOfAttribute>()
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

| Package                                                      | Description                      | .NET           |
|--------------------------------------------------------------|----------------------------------|----------------|
| **Core**                                                     |
| [FxMap][FxMap.nuget]                                         | Core library                     | 8.0, 9.0, 10.0 |
| **Data Providers**                                           |
| [FxMap.EntityFrameworkCore][FxMap.EntityFrameworkCore.nuget] | Entity Framework Core provider   | 8.0, 9.0, 10.0 |
| [FxMap.MongoDb][FxMap.MongoDb.nuget]                         | MongoDB provider                 | 8.0, 9.0, 10.0 |
| **Integrations**                                             |
| [FxMap.HotChocolate][FxMap.HotChocolate.nuget]               | HotChocolate GraphQL integration | 8.0, 9.0, 10.0 |
| **Transports**                                               |
| [FxMap.Grpc][FxMap.Grpc.nuget]                               | gRPC transport                   | 8.0, 9.0, 10.0 |
| [FxMap.Nats][FxMap.Nats.nuget]                               | NATS transport                   | 8.0, 9.0, 10.0 |
| [FxMap.RabbitMq][FxMap.RabbitMq.nuget]                       | RabbitMQ transport               | 8.0, 9.0, 10.0 |
| [FxMap.Kafka][FxMap.Kafka.nuget]                             | Kafka transport                  | 8.0, 9.0, 10.0 |
| [FxMap.Azure.ServiceBus][FxMap.Azure.ServiceBus.nuget]       | Azure Service Bus transport      | 8.0, 9.0, 10.0 |
| [FxMap.Aws.Sqs][FxMap.Aws.Sqs.nuget]                         | Amazon SQS transport             | 8.0, 9.0, 10.0 |
| **Tooling**                                                  |
| [FxMap.Analyzers][FxMap.Analyzers.nuget]                     | Roslyn analyzers                 | 8.0, 9.0, 10.0 |

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

[FxMap.nuget]: https://www.nuget.org/packages/FxMap/

[FxMap.EntityFrameworkCore.nuget]: https://www.nuget.org/packages/FxMap.EntityFrameworkCore/

[FxMap.MongoDb.nuget]: https://www.nuget.org/packages/FxMap.MongoDb/

[FxMap.HotChocolate.nuget]: https://www.nuget.org/packages/FxMap.HotChocolate/

[FxMap.Grpc.nuget]: https://www.nuget.org/packages/FxMap.Grpc/

[FxMap.Nats.nuget]: https://www.nuget.org/packages/FxMap.Nats/

[FxMap.RabbitMq.nuget]: https://www.nuget.org/packages/FxMap.RabbitMq/

[FxMap.Kafka.nuget]: https://www.nuget.org/packages/FxMap.Kafka/

[FxMap.Azure.ServiceBus.nuget]: https://www.nuget.org/packages/FxMap.Azure.ServiceBus/

[FxMap.Aws.Sqs.nuget]: https://www.nuget.org/packages/FxMap.Aws.Sqs/

[FxMap.Analyzers.nuget]: https://www.nuget.org/packages/FxMap.Analyzers/
