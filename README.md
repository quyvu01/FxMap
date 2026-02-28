# FxMap

```csharp
public string XId { get; set; }
[XOf(nameof(XId))] public string X { get; set; }
```

FxMap is an open-source library focused on Attribute-based data mapping. It streamlines data handling across services,
reduces boilerplate code, and improves maintainability.

**[Full Documentation](https://fxmapmapper.net)** | **[Getting Started](https://fxmapmapper.net/docs/getting-started)** |**[Expression Language](https://fxmapmapper.net/docs/expressions)**

> [!WARNING]
> All FxMap* packages need to have the same version.

## Quick Start

```bash
dotnet add package FxMap
```

```csharp
// 1. Configure FxMap
builder.Services.AddFxMap(cfg =>
{
    cfg.AddAttributesContainNamespaces(typeof(UserOfAttribute).Namespace!);
    cfg.AddModelConfigurationsFromNamespaceContaining<SomeModelAssemblyMarker>();
});

// 2. Define a custom FxMapAttribute
public sealed class UserOfAttribute(string propertyName) : FxMapAttribute(propertyName);

// 3. Configure the model
[FxMapConfigFor<UserOfAttribute>(nameof(Id), nameof(Name))]
public sealed class User
{
    public string Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
}

// 4. Use attributes in your DTOs
public sealed class SomeDataResponse
{
    public string UserId { get; set; }

    [UserOf(nameof(UserId))]
    public string UserName { get; set; }

    [UserOf(nameof(UserId), Expression = "Email")]
    public string UserEmail { get; set; }
}
```

## Key Features

- **Attribute-based Mapping**: Declarative data fetching using custom attributes
- **Powerful Expression Language**: SQL-like DSL for complex queries, filtering, aggregation, and projections
- **Multiple Data Providers**: Support for EF Core, MongoDB, and more
- **Multiple Transports**: gRPC, NATS, RabbitMQ, Kafka, Azure Service Bus
- **GraphQL Integration**: Seamless integration with HotChocolate

## Expression Examples

```csharp
// Simple property access
[UserOf(nameof(UserId), Expression = "Email")]
public string UserEmail { get; set; }

// Navigation properties
[ProvinceOf(nameof(ProvinceId), Expression = "Country.Name")]
public string CountryName { get; set; }

// Filtering
[UserOf(nameof(UserId), Expression = "Orders(Status = 'Done')")]
public List<OrderDTO> CompletedOrders { get; set; }

// Aggregation
[UserOf(nameof(UserId), Expression = "Orders:sum(Total)")]
public decimal TotalSpent { get; set; }

// Projection
[UserOf(nameof(UserId), Expression = "{Id, Name, Address.City as CityName}")]
public UserInfo UserDetails { get; set; }

// GroupBy
[UserOf(nameof(UserId), Expression = "Orders:groupBy(Status).{Status, :count as Count}")]
public List<OrderSummary> OrdersByStatus { get; set; }
```

For complete expression syntax including filters, indexers, functions, aggregations, boolean functions, coalesce,
ternary operators, and more, visit **[Expression Documentation](https://fxmapmapper.net/docs/expressions)**.

## Packages

| Package                                            | Description                      | .NET     |
|----------------------------------------------------|----------------------------------|----------|
| **Core**                                           |
| [FxMap][FxMap.nuget]                                   | Core library                     | 8.0, 9.0 |
| **Data Providers**                                 |
| [FxMap-EFCore][FxMap-EFCore.nuget]                     | Entity Framework Core provider   | 8.0, 9.0 |
| [FxMap-MongoDb][FxMap-MongoDb.nuget]                   | MongoDB provider                 | 8.0, 9.0 |
| **Integrations**                                   |
| [FxMap-HotChocolate][FxMap-HotChocolate.nuget]         | HotChocolate GraphQL integration | 8.0, 9.0 |
| **Transports**                                     |
| [FxMap-gRPC][FxMap-gRPC.nuget]                         | gRPC transport                   | 8.0, 9.0 |
| [FxMap-Nats][FxMap-Nats.nuget]                         | NATS transport                   | 8.0, 9.0 |
| [FxMap-RabbitMq][FxMap-RabbitMq.nuget]                 | RabbitMQ transport               | 8.0, 9.0 |
| [FxMap-Kafka][FxMap-Kafka.nuget]                       | Kafka transport                  | 8.0, 9.0 |
| [FxMap-Azure.ServiceBus][FxMap-Azure.ServiceBus.nuget] | Azure Service Bus transport      | 8.0, 9.0 |

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

[FxMap-EFCore.nuget]: https://www.nuget.org/packages/FxMap-EFCore/

[FxMap-MongoDb.nuget]: https://www.nuget.org/packages/FxMap-MongoDb/

[FxMap-HotChocolate.nuget]: https://www.nuget.org/packages/FxMap-HotChocolate/

[FxMap-gRPC.nuget]: https://www.nuget.org/packages/FxMap-gRPC/

[FxMap-Nats.nuget]: https://www.nuget.org/packages/FxMap-Nats/

[FxMap-RabbitMq.nuget]: https://www.nuget.org/packages/FxMap-RabbitMq/

[FxMap-Kafka.nuget]: https://www.nuget.org/packages/FxMap-Kafka/

[FxMap-Azure.ServiceBus.nuget]: https://www.nuget.org/packages/FxMap-Azure.ServiceBus/
