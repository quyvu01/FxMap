# FxMap
Effective distributed data mapping!
```csharp
public string UserId { get; set; }
public string UserName { get; set; }
public string UserEmail { get; set; }
```

FxMap is an open-source library focused on FluentAPI-based data mapping. It streamlines data handling across services,
reduces boilerplate code, and improves maintainability.

**[Full Documentation](https://fxmapmapper.net)** | **[Getting Started](https://fxmapmapper.net/docs/getting-started)** |**[Expression Language](https://fxmapmapper.net/docs/expressions)**

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
ternary operators, and more, visit **[Expression Documentation](https://fxmapmapper.net/docs/expressions)**.

## Packages

| Package                                                      | Description                      | .NET     |
|--------------------------------------------------------------|----------------------------------|----------|
| **Core**                                                     |
| [FxMap][FxMap.nuget]                                         | Core library                     | 8.0, 9.0 |
| **Data Providers**                                           |
| [FxMap.EntityFrameworkCore][FxMap.EntityFrameworkCore.nuget] | Entity Framework Core provider   | 8.0, 9.0 |
| [FxMap.MongoDb][FxMap.MongoDb.nuget]                         | MongoDB provider                 | 8.0, 9.0 |
| **Integrations**                                             |
| [FxMap.HotChocolate][FxMap.HotChocolate.nuget]               | HotChocolate GraphQL integration | 8.0, 9.0 |
| **Transports**                                               |
| [FxMap.Grpc][FxMap.Grpc.nuget]                               | gRPC transport                   | 8.0, 9.0 |
| [FxMap.Nats][FxMap.Nats.nuget]                               | NATS transport                   | 8.0, 9.0 |
| [FxMap.RabbitMq][FxMap.RabbitMq.nuget]                       | RabbitMQ transport               | 8.0, 9.0 |
| [FxMap.Kafka][FxMap.Kafka.nuget]                             | Kafka transport                  | 8.0, 9.0 |
| [FxMap.Azure.ServiceBus][FxMap.Azure.ServiceBus.nuget]       | Azure Service Bus transport      | 8.0, 9.0 |
| [FxMap.Aws.Sqs][FxMap.Aws.Sqs.nuget]                         | Amazon SQS transport             | 8.0, 9.0 |
| **Tooling**                                                  |
| [FxMap.Analyzers][FxMap.Analyzers.nuget]                     | Roslyn analyzers                 | 8.0, 9.0 |

## Documentation

Visit **[fxmapmapper.net](https://fxmapmapper.net)** for:

- [Getting Started Guide](https://fxmapmapper.net/docs/getting-started)
- [Configuration Options](https://fxmapmapper.net/docs/configuration)
- [Expression Language Reference](https://fxmapmapper.net/docs/expressions)
- [Data Provider Setup](https://fxmapmapper.net/docs/providers)
- [Transport Configuration](https://fxmapmapper.net/docs/transports)
- [API Reference](https://fxmapmapper.net/docs/api)

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
