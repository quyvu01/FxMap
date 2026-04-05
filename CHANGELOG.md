# Changelog

All notable changes to FxMap are documented here.
Versions follow [Semantic Versioning](https://semver.org/).
Detailed per-release notes live in [`docs/changelogs/`](docs/changelogs/).

---

## [2.0.0] - 2026-04-05

**Breaking release.** Upgrade from 1.x requires code changes — see the [full notes](docs/changelogs/v2.0.0.md).

### Breaking Changes

- **`UseDistributedKey` string overload** — second `namespace` parameter is now required.
  `UseDistributedKey<TKey>("Key")` → `UseDistributedKey("Key", "Namespace")`.
  Both values are validated by regex at startup.
- **`AddHandlersFromNamespaceContaining` removed** from `MapConfigurator`.
  Pipeline behaviors are the only handler registration path.
- **gRPC extension renamed** — `MapFxMapGrpcService()` → `MapFxMapperGrpc()`.
- **gRPC delegate renamed** — `GetFxMapResponseFunc` → `GetMapperResponseFunc`.
- **`GetAssemblyName()` extension removed** — use standard `AssemblyQualifiedName`.
- **Removed helpers** — `IsInterfaceOrConcreteClass`, `IsFSharpType`, `MessageRequestWrapped.Subject`.

### New

- **Namespace-scoped string keys** — dynamic types scoped to `{namespace}.{key}` preventing
  cross-service name collisions. `IFluentEntityConfig` gains `DistributedNamespace`.
- **Centralized type lookup** — `DistributedTypeData` record and
  `ConfigurationExtensions.GetDistributedTypeData` with `ConcurrentDictionary` cache replace
  per-transport `Type.GetType()` lookups. Required because dynamic types cannot be re-resolved
  by name at runtime.
- **`DistributedMapException`** — typed exception for catching mapping-specific runtime failures.

### Improvements

- gRPC discovery and dispatch pipeline is now fully string-based (`AssemblyQualifiedName`),
  making it compatible with dynamically-emitted types.
- `SqsRequestClient`, `NatsRequestClient`, `RabbitMqRequestClient` migrated to C# primary constructors.
- Sample services wired with gRPC HTTP/2 endpoints (`MapFxMapperGrpc`) as the default transport.

---

## [1.0.3] - 2026-03-15

No breaking changes. Internal improvements only — consuming projects require no code changes.

### Improvements

- **DI-first transport configuration** — all five transport packages replace internal `static`
  configuration classes (`NatsStatics`, `AzureServiceBusStatic`, `KafkaStatics`,
  `RabbitMqStatics`, `SqsStatics`) with properly registered singleton interfaces:
  `INatsConfiguration`, `IAzureServiceBusConfiguration`, `IKafkaConfiguration`,
  `IRabbitMqConfiguration`, `ISqsConfiguration`. All `Statics/` folders removed.
- **Subject / queue name resolution** moved from standalone `Extensions.cs` helpers into the
  configuration interfaces (`GetSubject`, `GetRequestQueue`, `GetReplyQueue`, `GetRequestTopic`,
  `GetQueueName`).
- **`FxMapStatics` eliminated** — `GetProfileConfig` / `GetEntityConfig` delegates are now
  registered as singletons in `AddFxMap()`.
- **EF Core exception fix** — missing entity config now throws
  `FxMapEntityFrameworkException.EntityConfigsMustNotBeEmpty` instead of a misleading
  `DistributedMapException.AddProfilesFromAssemblyContaining`.

---

## [1.0.2] - 2026-03-07

### Improvements

- Renamed all legacy "attribute" terminology to "distributedKey" across the entire codebase
  (exceptions, telemetry constants, proto fields, transport servers, core pipeline,
  data provider extensions). No behavioral changes.
- `PropertyInformation` renamed to `Property` for brevity; expression resolution
  parallelized with `Task.WhenAll`.

---

## [1.0.1] - 2026-02-XX

- Initial public release under the FxMap name.
- Logo and branding updated.

---

## [1.0.0] - 2026-02-XX

- First stable release. FluentAPI-based distributed data mapping across NATS, gRPC,
  RabbitMQ, Kafka, AWS SQS, and Azure Service Bus transports.
- EF Core and MongoDB data providers.
- HotChocolate GraphQL integration.
- Roslyn Analyzers (`FxMap.Analyzers`) for compile-time profile/entity validation.

---

For older OfX / pre-1.0 history see [`docs/changelogs/`](docs/changelogs/).
