# FxMap-RabbitMq

FxMap-RabbitMq is an extension package for FxMap that leverages RabbitMq for efficient data transportation. This package
provides a high-performance, strongly-typed communication layer for FxMap’s Attribute-based Data Mapping, enabling
streamlined data retrieval across distributed systems.

[Demo Project!](https://github.com/quyvu01/TestFxMap-Demo)

---

## Introduction

RabbitMq-based Transport: Implements RabbitMq to handle data communication between services, providing a fast, secure,
and scalable solution.

---

## Installation

To install the FxMap-RabbitMq package, use the following NuGet command:

```bash
dotnet add package FxMap-RabbitMq
```

Or via the NuGet Package Manager:

```bash
Install-Package FxMap-RabbitMq
```

---

## How to Use

### 1. Register FxMap-RabbitMq

Add FxMap-RabbitMq to your service configuration during application startup:

```csharp
builder.Services.AddFxMap(cfg =>
{
    cfg.AddContractsContainNamespaces(typeof(SomeContractAssemblyMarker).Assembly);
    cfg.AddRabbitMq(config => config.Host("localhost", "/")); // This is usally used for local test
    // Or fullly configuration as bellow:
    cfg.AddRabbitMq(config => config.Host("localhost", "/", 5672, c =>
    {
        c.UserName("SomeUserName");
        c.Password("SomePassword");
    }));
});

...

var app = builder.Build();

app.Run();

```

`Note:` FxMap-RabbitMq uses exchanges that start with `FxMap-[FxMapAttribute metadata]`. Therefore, you should avoid using
other exchanges. Additionally, FxMap-RabbitMq automatically creates the queue `fxmap-rpc-queue-[application friendly name]`,
so you should avoid creating a queue with the same name in your application.

That All, enjoy your moment!

| Package Name                                       | Description                                                                                                             | .NET Version | Document                                                                                 |
|----------------------------------------------------|-------------------------------------------------------------------------------------------------------------------------|--------------|------------------------------------------------------------------------------------------|
| **Core**                                           |                                                                                                                         |
| [FxMap][FxMap.nuget]                                   | FxMap core                                                                                                                | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/README.md)                             |
| **Data Providers**                                 |                                                                                                                         |
| [FxMap-EFCore][FxMap-EFCore.nuget]                     | This is the FxMap extension package using EntityFramework to fetch data                                                   | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.EntityFrameworkCore/README.md) |
| [FxMap-MongoDb][FxMap-MongoDb.nuget]                   | This is the FxMap extension package using MongoDb to fetch data                                                           | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.MongoDb/README.md)             |
| **Integrations**                                   |                                                                                                                         |
| [FxMap-HotChocolate][FxMap-HotChocolate.nuget]         | FxMap.HotChocolate is an integration package with HotChocolate for FxMap.                                                   | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.HotChocolate/README.md)        |
| **Transports**                                     |                                                                                                                         |
| [FxMap-Aws.Sqs][FxMap-Aws.Sqs.nuget]                   | FxMap-Aws-Sqs is an extension package for FxMap that leverages Amazon SQS for efficient data transportation.                | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Aws.Sqs/README.md)             |
| [FxMap-Azure.ServiceBus][FxMap-Azure.ServiceBus.nuget] | FxMap.Azure.ServiceBus is an extension package for FxMap that leverages Azure ServiceBus for efficient data transportation. | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Azure.ServiceBus/README.md)    |
| [FxMap-gRPC][FxMap-gRPC.nuget]                         | FxMap.gRPC is an extension package for FxMap that leverages gRPC for efficient data transportation.                         | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Grpc/README.md)                |
| [FxMap-Kafka][FxMap-Kafka.nuget]                       | FxMap-Kafka is an extension package for FxMap that leverages Kafka for efficient data transportation.                       | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Kafka/README.md)               |
| [FxMap-Nats][FxMap-Nats.nuget]                         | FxMap-Nats is an extension package for FxMap that leverages Nats for efficient data transportation.                         | 8.0, 9.0     | [ReadMe](https://github.com/quyvu01/FxMap/blob/main/src/FxMap.Nats/README.md)                |
| [FxMap-RabbitMq][FxMap-RabbitMq.nuget]                 | FxMap-RabbitMq is an extension package for FxMap that leverages RabbitMq for efficient data transportation.                 | 8.0, 9.0     | This Document                                                                            |

---

[FxMap.nuget]: https://www.nuget.org/packages/FxMap/

[FxMap-EFCore.nuget]: https://www.nuget.org/packages/FxMap-EFCore/

[FxMap-MongoDb.nuget]: https://www.nuget.org/packages/FxMap-MongoDb/

[FxMap-HotChocolate.nuget]: https://www.nuget.org/packages/FxMap-HotChocolate/

[FxMap-Aws.Sqs.nuget]: https://www.nuget.org/packages/FxMap-Aws.Sqs/

[FxMap-gRPC.nuget]: https://www.nuget.org/packages/FxMap-gRPC/

[FxMap-Nats.nuget]: https://www.nuget.org/packages/FxMap-Nats/

[FxMap-RabbitMq.nuget]: https://www.nuget.org/packages/FxMap-RabbitMq/

[FxMap-Kafka.nuget]: https://www.nuget.org/packages/FxMap-Kafka/

[FxMap-Azure.ServiceBus.nuget]: https://www.nuget.org/packages/FxMap-Azure.ServiceBus/