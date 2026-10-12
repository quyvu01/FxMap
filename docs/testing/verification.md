# What FxMap is tested against

*Generated from a full run of the test suite with the real infrastructure below (.NET 10 run; CI runs the same suite on .NET 8, 9 and 10). Counts are exact for that run.*

## At a glance

| | |
|---|---|
| Automated tests | **826** (809 in the main suite + 17 analyzer tests) |
| Result of the last full run | **803 passed, 0 failed**, 6 run only on the Service Bus emulator in CI |
| .NET versions | 8, 9, 10 (every test runs on all three in CI) |
| Data providers tested against a real database | Entity Framework Core (PostgreSQL 17), MongoDB 7 |
| Transports tested between two services over a real broker | gRPC, NATS, RabbitMQ, Apache Kafka, Amazon SQS (LocalStack), Azure Service Bus (emulator) |
| Line coverage of the main suite | **62%** overall (FxMap core 67%, EF Core provider 88%, RabbitMQ 86%, SQS 87%, NATS 83%, Kafka 70%) |

## The test suite

| Area | Tests | What it checks |
|---|---:|---|
| Unit: mapper | 334 | Discovery of the properties to map in object graphs (collections, cycles, shared objects, nesting depth, byte arrays), request batching by key and level, dependency order (`UserId → ProvinceId → CountryId`), conditional expressions, cancellation, headers, computed keys, expression errors |
| Unit: expression language | 232 | Parser and builders for navigation, filters, indexers, aggregations, projections, groupBy, functions |
| Unit: helpers, services, fluent, accessors, gRPC | 74 | Id parsing and canonical spellings, entity/profile validation, accessors, host configuration |
| Contract | 8 | Request/response contract of the transports |
| Integration: expression language on EF Core | 63 | Every expression feature translated to LINQ and executed |
| Integration: EF Core mapping | 38 | Entity configurations, computed ids, expression errors in both modes |
| Provider tests on real databases | 24 | 12 on PostgreSQL through EF Core and 12 on MongoDB: the same scenarios for both (below) |
| Transport tests over real brokers | 36 | 6 for each of gRPC, NATS, RabbitMQ, Kafka, SQS, Service Bus (below) |
| Roslyn analyzer | 17 | Compile-time validation of expression strings |

## Data providers (real PostgreSQL and MongoDB)

The same scenarios run against both providers, behind the real registration (`AddFxMap(...).AddEntityFrameworkCore(...)` / `AddMongoDb(...)`):

- string, `Guid` and `int` ids, answered under every spelling the caller used (upper case, no hyphens, leading zeros);
- ids that do not exist, no ids, the same id twice;
- navigation, aggregates (`Orders:count`, `Orders:sum(Total)`, with filters), a missing nested object and an empty collection;
- the mapper filling a view end to end;
- a computed id (`Id(x => x.Mrn + x.Code)`) on EF Core, and its documented limit on MongoDB;
- an expression the owner cannot build is reported once in the response, the other expressions are still answered, also when no row matches.

## Transports (two services, one real broker)

For each transport an *owner* service and a *consumer* service run in one test process and only talk through the broker:

| Check | gRPC | NATS | RabbitMQ | Kafka | SQS | Service Bus |
|---|:--:|:--:|:--:|:--:|:--:|:--:|
| Values mapped from the owner; three objects make one request | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ (CI) |
| Headers of the context reach the owner | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ (CI) |
| An error of the owner reaches the consumer | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ (CI) |
| No owner: the request times out | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ (CI) |
| Expression errors reported by the owner arrive | ✓ | ✓ | ✓ | ✓ | ✓ | written, not yet run on the emulator |
| 200 concurrent requests, each gets its own answer | ✓ | ✓ | ✓ | ✓ | ✓ | written, not yet run on the emulator |

Brokers: gRPC (Kestrel, in process), NATS 2, RabbitMQ 3, Apache Kafka, LocalStack 3 (SQS), Azure Service Bus emulator. "(CI)": runs in GitHub Actions on the emulator (the emulator does not run on Apple Silicon machines). The last two Service Bus tests were added after the last CI run on the emulator, so they have not run yet.

## Load test on the sample services

Three sample services (a REST/GraphQL API, two data owners backed by PostgreSQL and MongoDB) and `GET /api/Test/GetMembers`, which maps a member through several distributed keys and dependent levels (user → province → country). 16 concurrent clients (the RabbitMQ rows list their own), on one laptop with local brokers, every response checked for the right data:

| Transport | Requests | Correct answers | Time |
|---|---:|---:|---:|
| gRPC | 2,000 | 2,000 | 4.4 s |
| NATS | 2,000, run twice | 4,000 of 4,000 | 4.2 s and 5.1 s |
| RabbitMQ | 8,000 (2,000 with 8 clients, 3,000 with 16, 3,000 with 32) | 8,000 | 7.5 s, 7.0 s, 5.7 s |
| Kafka | 2,000 | 2,000 | 26.8 s |
| SQS (LocalStack) | 2,000 | 2,000 | 167 s |

Indicative only: one machine, local brokers, databases included. Kafka and SQS time is dominated by the broker and by long polling, not by FxMap.

## Mapper benchmark

Against AutoMapper 14 `ProjectTo` on EF Core InMemory (see the README): on par or faster from about 100 objects, at a half to a third of the memory.

## Bugs that this testing found and fixed

Running the providers and transports for real, end to end, found defects that unit tests could not:

- **MongoDB:** `Guid` ids were never answered; values came back as extended JSON (`{ "$numberDecimal": ... }`) that cannot be read into a `decimal`.
- **RabbitMQ:** headers of the context were lost (`System.Byte[]`); under load a request could read another message's reply (found by a load test).
- **Kafka:** the process crashed when a service stopped; headers were never sent; a reply could be lost; disposing a client threw.
- **gRPC:** `ThrowIfException()` had no effect on the clients when it was called after `AddGrpcClients`.
- **SQS** and **NATS:** no defect found.
