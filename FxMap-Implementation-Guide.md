# FxMap Implementation Guide

FxMap is a FluentAPI-based distributed data enrichment library for .NET microservices. Given a DTO with a foreign-key property (e.g., `UserId`), it automatically fetches and populates related properties (`UserName`, `UserEmail`) from the owning service — across any message transport or HTTP/gRPC — without writing boilerplate query code.

---

## Core Concepts

| Concept | Class | Role |
|---|---|---|
| Distributed Key | `IDistributedKey` | Marker that identifies which remote service owns an entity type |
| Entity config | `EntityConfigureOf<T>` | Declares how an entity exposes itself to the mapping engine |
| DTO profile | `ProfileOf<T>` | Maps a key field on a DTO to target fields, with optional expressions |
| Expression DSL | `string` | SQL-like queries: navigation, filters, aggregations, projections |

---

## 1. Package References

Install only what each service needs.

```xml
<!-- Every service that maps DTOs -->
<PackageReference Include="FxMap" Version="x.y.z" />

<!-- Data providers — pick one or both per service -->
<PackageReference Include="FxMap.EntityFrameworkCore" Version="x.y.z" />
<PackageReference Include="FxMap.MongoDb" Version="x.y.z" />

<!-- Transports — pick one per service (or mix client/server) -->
<PackageReference Include="FxMap.Nats" Version="x.y.z" />
<PackageReference Include="FxMap.Grpc" Version="x.y.z" />
<PackageReference Include="FxMap.RabbitMq" Version="x.y.z" />
<PackageReference Include="FxMap.Kafka" Version="x.y.z" />
<PackageReference Include="FxMap.Azure.ServiceBus" Version="x.y.z" />
<PackageReference Include="FxMap.Aws.Sqs" Version="x.y.z" />

<!-- Optional extras -->
<PackageReference Include="FxMap.HotChocolate" Version="x.y.z" />
<PackageReference Include="FxMap.Analyzers" Version="x.y.z" PrivateAssets="all" />
```

---

## 2. Distributed Keys

Define one sealed class per entity type. Place these in a shared contracts project so both the provider service and consumer service can reference the same type.

```csharp
// Shared.Contracts/Attributes/Keys.cs
public sealed class UserOfAttribute : IDistributedKey;
public sealed class ProductOfAttribute : IDistributedKey;
public sealed class OrderOfAttribute : IDistributedKey;
```

**Loose coupling alternative** — if services cannot share a binary, use string keys instead. The string key and namespace must match on both sides:

```csharp
// No shared type needed
UseDistributedKey("UserOf", "MyCompany.Services.UserService");
```

---

## 3. Entity Configuration (Provider Service)

The service that owns the data declares an `EntityConfigureOf<T>` for each entity it exposes.

```csharp
// UserService/Models/User.cs
public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public int CountryId { get; set; }
    public Country Country { get; set; }
}

public class UserConfig : EntityConfigureOf<User>
{
    protected override void Configure()
    {
        Id(x => x.Id);                             // Required: primary key
        DefaultProperty(x => x.Name);              // Returned when no expression specified
        UseDistributedKey<UserOfAttribute>();       // Links this entity to the key type
        ExposedName(x => x.Email, "UserEmail");    // Optional: alias a property
    }
}
```

Rules:
- Every entity must have exactly one `Id(...)` call.
- `DefaultProperty` is used when a `.For(...)` call has no expression string.
- One `EntityConfigureOf<T>` per entity; one distributed key per entity type.

---

## 4. DTO Profile (Consumer Service)

The service consuming the data declares a `ProfileOf<T>` for each DTO that needs enrichment.

```csharp
// ConsumerService/Responses/UserSummaryDto.cs
public class UserSummaryDto
{
    public Guid UserId { get; set; }       // Key field (foreign key)
    public string UserName { get; set; }   // To be populated
    public string UserEmail { get; set; }  // To be populated
    public string CountryName { get; set; }
}

public class UserSummaryProfile : ProfileOf<UserSummaryDto>
{
    protected override void Configure()
    {
        UseDistributedKey<UserOfAttribute>()
            .Of(x => x.UserId)                          // Selector (foreign key)
            .For(x => x.UserName)                       // Maps to DefaultProperty (Name)
            .For(x => x.UserEmail, "UserEmail")         // Maps to the exposed alias
            .For(x => x.CountryName, "Country.Name");   // Navigation expression
    }
}
```

---

## 5. Registration

### Provider service (owns data, no profiles needed)

```csharp
builder.Services
    .AddFxMap(cfg =>
    {
        cfg.AddEntitiesFromAssemblyContaining<IAssemblyMarker>();
        cfg.AddNats(c => c.NatsOpts(opts => opts.Url = "nats://localhost:4222"));
        cfg.ThrowIfException();
    })
    .AddEntityFrameworkCore(cfg =>
        cfg.AddDbContexts(typeof(AppDbContext)));
```

### Consumer service (maps DTOs, no entities needed)

```csharp
builder.Services
    .AddFxMap(cfg =>
    {
        cfg.AddProfilesFromAssemblyContaining<IAssemblyMarker>();
        cfg.AddNats(c => c.NatsOpts(opts => opts.Url = "nats://localhost:4222"));
        cfg.ThrowIfException();
    });
```

### Service that is both provider and consumer

```csharp
builder.Services
    .AddFxMap(cfg =>
    {
        cfg.AddEntitiesFromAssemblyContaining<IAssemblyMarker>();
        cfg.AddProfilesFromAssemblyContaining<IAssemblyMarker>();
        cfg.AddNats(c => c.NatsOpts(opts => opts.Url = "nats://localhost:4222"));
        cfg.ThrowIfException();
    })
    .AddEntityFrameworkCore(cfg => cfg.AddDbContexts(typeof(AppDbContext)));
```

### Optional configuration knobs

```csharp
cfg.SetMaxNestingDepth(128);                      // Prevent infinite recursion (default: 128)
cfg.SetMaxConcurrentProcessing(256);              // Backpressure for message transports
cfg.SetRequestTimeOut(TimeSpan.FromSeconds(60));  // Per-request timeout (default: 30s)
cfg.SetRetryPolicy(3, attempt => TimeSpan.FromMilliseconds(200 * attempt), onRetry: null);
cfg.ConfigureSupervisor(opts =>
{
    opts.Strategy = SupervisionStrategy.OneForOne;
    opts.MaxRestarts = 5;
    opts.EnableCircuitBreaker = true;
});
```

---

## 6. Transport Reference

| Transport | Package | Registration snippet |
|---|---|---|
| **NATS** | `FxMap.Nats` | `cfg.AddNats(c => c.NatsOpts(o => o.Url = "nats://host:4222"))` |
| **gRPC** | `FxMap.Grpc` | Client: `cfg.AddGrpcClients(g => g.AddGrpcHosts("https://svc:5001"))` · Server: `app.MapFxMapperGrpc()` |
| **RabbitMQ** | `FxMap.RabbitMq` | `cfg.AddRabbitMq(c => c.Host("h").Port(5672).Credential("u","p"))` |
| **Kafka** | `FxMap.Kafka` | `cfg.AddKafka(c => c.KafkaHost("host:9092"))` |
| **Azure Service Bus** | `FxMap.Azure.ServiceBus` | `cfg.AddAzureServiceBus(c => c.ConnectionString("..."))` |
| **AWS SQS** | `FxMap.Aws.Sqs` | `cfg.AddAwsSqs(c => c.Region("us-east-1"))` |

All message-based transports (everything except gRPC) use the supervisor pattern and support configurable backpressure and circuit-breaker options.

---

## 7. Expression DSL Reference

Expressions are `string` values passed to `.For(x => x.Prop, "expression")`.

### Property access and navigation

```
Name                           // Simple property
Country.Province.City.Name     // Navigation chain
Country?.Province?.City.Name   // Null-safe navigation
```

### Filtering collections

```
Orders(Status = 'Done')
Orders(Status = 'Done' && Total > 100)
Orders(Status = 'Pending' || Status = 'Waiting')
Orders(Year = 2024, Status = 'Active')     // Comma means AND
```

### Indexers and sorting

```
Orders[0 asc OrderDate]                    // First item, ascending
Orders[-1 desc OrderDate]                  // Last item, descending
Orders[0 10 asc OrderDate]                 // First 10 items
Orders(Status='Done')[0 5 desc Total]      // Filter then paginate
```

### Functions (chained with `:`)

```
Name:upper                     // "JOHN"
Email:lower:trim               // Chained
CreatedAt:year                 // 2024
CreatedAt:format('yyyy-MM-dd') // "2024-12-25"
CreatedAt:daysAgo
Price:round(2)
Price:add(Tax):round(2)
Items:count
Items:distinct(Name):count
```

### Aggregations

```
Orders:count
Orders:sum(Total)
Orders:avg(Total)
Orders:min(Total)
Orders:max(Total)
Orders(Status='Done'):sum(Total)   // Aggregate with filter
```

### Projections

```
{Id, Name, Email}
{Id, Country.Name as CountryName}
Orders.{Id, Total:round(2) as Amount}
{(Nickname ?? Name) as DisplayName}
```

### Coalesce and ternary

```
FirstName ?? LastName ?? 'Unknown'
Status = 'Done' ? 'Completed' : 'Pending'
```

### GroupBy

```
Orders:groupBy(Status).{Status, :count as Count}
```

---

## 8. Conditional Mapping

Use the builder overload when the expression to use depends on runtime state.

```csharp
.For(x => x.UserName, c => c
    .If(serviceProvider => CheckSomeCondition(serviceProvider))
    .Expression("UserEmail:upper")   // Used when If() is true
    .Else("Name"))                   // Fallback
```

`If` and `Else` both accept sync or `async` delegates.

---

## 9. Multi-Level Profiles (Chaining)

Profiles compose automatically. If `MemberResponse` contains `ProvinceId` which is populated by a first mapping pass, a second key can use it immediately:

```csharp
public class MemberResponseProfile : ProfileOf<MemberResponse>
{
    protected override void Configure()
    {
        // First pass: populate ProvinceId from UserId
        UseDistributedKey<UserOfAttribute>()
            .Of(x => x.UserId)
            .For(x => x.ProvinceId, "ProvinceId");

        // Second pass: use the now-populated ProvinceId
        UseDistributedKey<ProvinceOfAttribute>()
            .Of(x => x.ProvinceId)
            .For(x => x.ProvinceName)
            .For(x => x.CountryName, "Country.Name");
    }
}
```

FxMap resolves dependency graphs automatically; ordering within `Configure()` does not matter.

---

## 10. MongoDB Provider

Register specific collections explicitly (unlike EF Core which scans DbContext).

```csharp
var mongoClient = new MongoClient(connectionString);
var db = mongoClient.GetDatabase("mydb");

builder.Services
    .AddFxMap(cfg => { /* ... */ })
    .AddMongoDb(cfg =>
    {
        cfg.AddCollection(db.GetCollection<User>("users"));
        cfg.AddCollection(db.GetCollection<Product>("products"));
    });
```

---

## 11. HotChocolate / GraphQL Integration

```csharp
builder.Services
    .AddFxMap(cfg =>
    {
        // ...
    })
    .AddHotChocolate(cfg =>
        cfg.AddRequestExecutorBuilder(services =>
            services.AddGraphQL().AddQueryType<Query>()));
```

FxMap hooks into HotChocolate's execution pipeline and enriches response objects automatically before serialization.

---

## 12. Roslyn Analyzer

Add `FxMap.Analyzers` to the project that declares entity configs and profiles. It provides compile-time diagnostics for:
- Missing `Id(...)` on `EntityConfigureOf<T>`
- Duplicate distributed key assignments
- Profile referencing a key that has no entity config
- Invalid expression strings (partial)

```xml
<PackageReference Include="FxMap.Analyzers" Version="x.y.z" PrivateAssets="all" />
```

---

## 13. Typical Multi-Service Layout

```
Solution/
├── Shared.Contracts/          # IDistributedKey implementations + shared DTOs
│   └── Keys/
│       ├── UserOfAttribute.cs
│       └── ProductOfAttribute.cs
│
├── UserService/               # Data provider
│   ├── Models/
│   │   └── User.cs            # Entity + UserConfig : EntityConfigureOf<User>
│   └── Program.cs             # AddFxMap + AddEntityFrameworkCore, no profiles
│
├── ProductService/            # Data provider
│   ├── Models/
│   │   └── Product.cs         # Entity + ProductConfig : EntityConfigureOf<Product>
│   └── Program.cs
│
└── ApiGateway/                # Consumer
    ├── Responses/
    │   └── OrderDto.cs        # DTO + OrderDtoProfile : ProfileOf<OrderDto>
    └── Program.cs             # AddFxMap + profiles, no entities, no data provider
```

---

## 14. Checklist for a New Service

**Provider service (exposes data)**
- [ ] Reference `FxMap` + one data-provider package
- [ ] Reference a transport package
- [ ] Create one `EntityConfigureOf<T>` per exposed entity
- [ ] Call `AddEntitiesFromAssemblyContaining<T>()` in registration
- [ ] Register the data provider (`.AddEntityFrameworkCore(...)` or `.AddMongoDb(...)`)

**Consumer service (enriches DTOs)**
- [ ] Reference `FxMap` + a transport package
- [ ] Create one `ProfileOf<T>` per DTO that needs enrichment
- [ ] Call `AddProfilesFromAssemblyContaining<T>()` in registration
- [ ] Inject `IDistributedMapper` and call `MapAsync(dto)` / `MapManyAsync(dtos)`

**Both sides**
- [ ] Share or replicate the `IDistributedKey` marker types
- [ ] Use the same transport and connection string / endpoint
- [ ] Set `ThrowIfException()` during development; remove or handle gracefully in production