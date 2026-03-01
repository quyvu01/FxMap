# FxMap.Azure.ServiceBus

FxMap.Azure.ServiceBus is an extension package for **FxMap** that leverages **Azure Service Bus** for reliable and scalable
message transportation.
This package provides a **strongly-typed**, **cloud-native** communication layer for FxMap's **FluentAPI-based Data
Mapping**, enabling seamless data transfer across distributed systems using Microsoft Azure infrastructure.

> [!WARNING]
> The Azure Service Bus transport only supports Standard and Premium tiers of the Microsoft Azure Service Bus service. Premium tier is recommended for production environments.

[Demo Project!](https://github.com/quyvu01/TestFxMap-Demo)

---

## Introduction

**Azure Service Bus-based Transport:**
Implements Azure Service Bus to handle data communication between distributed FxMap services, providing an
enterprise-grade, secure, and scalable messaging backbone with features like topics, queues, and session management.

---

## Installation

To install the **FxMap.Azure.ServiceBus** package, use the following NuGet command:

```csharp
dotnet add package FxMap.Azure.ServiceBus
```

Or via the NuGet Package Manager:

```csharp
Install-Package FxMap.Azure.ServiceBus
```

## How to Use

### 1. Register FxMap.Azure.ServiceBus

Add FxMap.Azure.ServiceBus to your service configuration during application startup:

Example

```csharp
builder.Services.AddFxMap(cfg =>
    {
        cfg.AddEntitiesFromAssemblyContaining<SomeEntityAssemblyMarker>();
        cfg.AddProfilesFromAssemblyContaining<SomeProfileAssemblyMarker>();
        cfg.AddAzureServiceBus(c => c.Host("SensitiveConnectionString"));
    });
...

var app = builder.Build();

...

app.Run();
```

`Note:` FxMap.Azure.ServiceBus uses message subjects that start with fxmap-request-[IDistributedKey metadata].
You should avoid using other queues or topics with the same naming pattern.

The package supports both queue-based and topic-based messaging models.

When RequiresSession is enabled, all messages will be processed in a sessionful mode, ensuring ordered delivery.

That's all -- enjoy building your distributed system with FxMap!

| Package Name                                                   | Description                                                                                                                | .NET Version | Document                                                                                    |
|----------------------------------------------------------------|----------------------------------------------------------------------------------------------------------------------------|--------------|---------------------------------------------------------------------------------------------|
| **Core**                                                       |                                                                                                                            |
| [FxMap][FxMap.nuget]                                           | FxMap core                                                                                                                 | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/README.md)                             |
| **Data Providers**                                             |                                                                                                                            |
| [FxMap.EntityFrameworkCore][FxMap.EntityFrameworkCore.nuget]    | FxMap extension package using EntityFramework to fetch data                                                                | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.EntityFrameworkCore/README.md) |
| [FxMap.MongoDb][FxMap.MongoDb.nuget]                           | FxMap extension package using MongoDb to fetch data                                                                        | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.MongoDb/README.md)            |
| **Integrations**                                               |                                                                                                                            |
| [FxMap.HotChocolate][FxMap.HotChocolate.nuget]                 | FxMap.HotChocolate is an integration package with HotChocolate for FxMap.                                                  | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.HotChocolate/README.md)       |
| **Transports**                                                 |                                                                                                                            |
| [FxMap.Aws.Sqs][FxMap.Aws.Sqs.nuget]                          | FxMap.Aws.Sqs is an extension package for FxMap that leverages Amazon SQS for efficient data transportation.               | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Aws.Sqs/README.md)            |
| [FxMap.Azure.ServiceBus][FxMap.Azure.ServiceBus.nuget]         | FxMap.Azure.ServiceBus is an extension package for FxMap that leverages Azure ServiceBus for efficient data transportation. | 8.0, 9.0     | This Document                                                                               |
| [FxMap.Grpc][FxMap.Grpc.nuget]                                 | FxMap.Grpc is an extension package for FxMap that leverages gRPC for efficient data transportation.                        | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Grpc/README.md)               |
| [FxMap.Kafka][FxMap.Kafka.nuget]                               | FxMap.Kafka is an extension package for FxMap that leverages Kafka for efficient data transportation.                      | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Kafka/README.md)              |
| [FxMap.Nats][FxMap.Nats.nuget]                                 | FxMap.Nats is an extension package for FxMap that leverages Nats for efficient data transportation.                        | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Nats/README.md)               |
| [FxMap.RabbitMq][FxMap.RabbitMq.nuget]                         | FxMap.RabbitMq is an extension package for FxMap that leverages RabbitMq for efficient data transportation.                | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.RabbitMq/README.md)           |

---

[FxMap.nuget]: https://www.nuget.org/packages/FxMap/

[FxMap.EntityFrameworkCore.nuget]: https://www.nuget.org/packages/FxMap.EntityFrameworkCore/

[FxMap.MongoDb.nuget]: https://www.nuget.org/packages/FxMap.MongoDb/

[FxMap.HotChocolate.nuget]: https://www.nuget.org/packages/FxMap.HotChocolate/

[FxMap.Aws.Sqs.nuget]: https://www.nuget.org/packages/FxMap.Aws.Sqs/

[FxMap.Grpc.nuget]: https://www.nuget.org/packages/FxMap.Grpc/

[FxMap.Nats.nuget]: https://www.nuget.org/packages/FxMap.Nats/

[FxMap.RabbitMq.nuget]: https://www.nuget.org/packages/FxMap.RabbitMq/

[FxMap.Kafka.nuget]: https://www.nuget.org/packages/FxMap.Kafka/

[FxMap.Azure.ServiceBus.nuget]: https://www.nuget.org/packages/FxMap.Azure.ServiceBus/
